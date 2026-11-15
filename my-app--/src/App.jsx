import { useEffect, useState } from 'react'
import axios from 'axios'
import bookLogo from '/logo.png'
import './App.css'

const API_URL = 'http://localhost:8939/api'

function App() {
  const [books, setBooks] = useState([])
  const [accountOptions, setAccountOptions] = useState([])

  const [selectedBookName, setSelectedBookName] = useState('')
  const [author, setAuthor] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [account, setAccount] = useState('')
  const [userId, setUserId] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [purchaseStatus, setPurchaseStatus] = useState('')

  useEffect(() => {
    loadBooks()
    loadAccounts()
  }, [])

  const loadBooks = async () => {
    try {
      const response = await axios.get(
        `${API_URL}/BackendSF/books`
      )

      setBooks(response.data)

      if (response.data.length > 0) {
        setSelectedBookName(response.data[0].name)
        setAuthor(response.data[0].author)
      }
    } catch (error) {
      console.error(
        'Greška pri učitavanju knjiga:',
        error
      )
    }
  }

  const loadAccounts = async () => {
    try {
      const response = await axios.get(
        `${API_URL}/BackendSF/accounts`
      )

      setAccountOptions(response.data)

      if (response.data.length > 0) {
        const firstAccount = response.data[0]
        const accountValue =
          firstAccount.account ||
          firstAccount.Account

        setAccount(accountValue)
        setUserId(accountValue)
      }
    } catch (error) {
      console.error(
        'Greška pri učitavanju naloga:',
        error
      )
    }
  }

  const handleBookChange = (event) => {
    const bookName = event.target.value

    setSelectedBookName(bookName)

    const selectedBook = books.find(
      (book) => book.name === bookName
    )

    if (selectedBook) {
      setAuthor(selectedBook.author)
    }
  }

  const handleAccountChange = (event) => {
    const selectedAccount = event.target.value

    setAccount(selectedAccount)
    setUserId(selectedAccount)
  }

  const isTransientError = (error) => {
    if (!error.response) {
      return true
    }

    return error.response.status >= 500 ||
      error.response.status === 429
  }

  const wait = (milliseconds) => {
    return new Promise((resolve) => {
      setTimeout(resolve, milliseconds)
    })
  }

  const startPurchaseWithRetry = async (
    purchaseId,
    selectedBook
  ) => {
    const requestBody = {
      userId,
      bookId: selectedBook.bookId,
      quantity
    }

    const maxRetries = 3

    for (let attempt = 0; attempt <= maxRetries; attempt++) {
      try {
        return await axios.post(
          `${API_URL}/purchases`,
          requestBody,
          {
            headers: {
              'Idempotency-Key': purchaseId
            }
          }
        )
      } catch (error) {
        const canRetry =
          isTransientError(error) &&
          attempt < maxRetries

        if (!canRetry) {
          throw error
        }

        const delay =
          500 * Math.pow(2, attempt)

        await wait(delay)
      }
    }

    throw new Error(
      'Kupovina nije pokrenuta.'
    )
  }

  const waitForPurchase = async (purchaseId) => {
    const maxChecks = 30

    for (let attempt = 0; attempt < maxChecks; attempt++) {
      await wait(1000)

      const response = await axios.get(
        `${API_URL}/purchases/${purchaseId}`
      )

      const purchase = response.data
      setPurchaseStatus(purchase.status)

      if (
        purchase.status === 'Completed' ||
        purchase.status === 'Compensated' ||
        purchase.status === 'Failed' ||
        purchase.status === 4 ||
        purchase.status === 6 ||
        purchase.status === 7
      ) {
        return purchase
      }
    }

    throw new Error(
      'Praćenje kupovine je isteklo.'
    )
  }

  const handleSubmit = async (event) => {
    event.preventDefault()

    const selectedBook = books.find(
      (book) => book.name === selectedBookName
    )

    if (!selectedBook) {
      alert('Knjiga nije pronađena.')
      return
    }

    if (!userId) {
      alert('Korisnički nalog nije izabran.')
      return
    }

    if (!Number.isInteger(quantity) || quantity <= 0) {
      alert('Količina mora biti pozitivan ceo broj.')
      return
    }

    const purchaseId = crypto.randomUUID()

    setIsSubmitting(true)
    setPurchaseStatus('Started')

    try {
      await startPurchaseWithRetry(
        purchaseId,
        selectedBook
      )

      const purchase =
        await waitForPurchase(purchaseId)

      const finalStatus = purchase.status

      if (
        finalStatus === 'Completed' ||
        finalStatus === 4
      ) {
        alert(
          'Kupovina je uspešno završena.'
        )

        await loadBooks()
        await loadAccounts()
      } else if (
        finalStatus === 'Compensated' ||
        finalStatus === 6
      ) {
        alert(
          'Kupovina nije uspela. ' +
          'Promene su kompenzovane.'
        )
      } else {
        alert(
          'Kupovina nije završena. Status: ' +
          finalStatus
        )
      }

      setPurchaseStatus(finalStatus)
    } catch (error) {
      console.error(
        'Greška pri kupovini:',
        error
      )

      if (error.response) {
        const message =
          error.response.data?.message ||
          error.response.data

        alert(
          'Greška servera: ' + message
        )
      } else {
        alert(
          'Server nije dostupan.'
        )
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <>
      <div className="header">
        <h1>BookShop</h1>

        <img
          src={bookLogo}
          alt="Book logo"
          className="logo"
        />
      </div>

      <form
        className="book-form"
        onSubmit={handleSubmit}
      >
        <div className="form-group">
          <label htmlFor="name">
            Book name:
          </label>

          <select
            id="name"
            value={selectedBookName}
            onChange={handleBookChange}
            disabled={isSubmitting}
          >
            {books.map((book) => (
              <option
                key={book.bookId}
                value={book.name}
              >
                {book.name} (
                Price: {book.price} RSD,
                Stock: {book.quantity})
              </option>
            ))}
          </select>
        </div>

        <div className="form-group">
          <label htmlFor="author">
            Author:
          </label>

          <input
            id="author"
            type="text"
            disabled
            value={author}
          />
        </div>

        <div className="form-group">
          <label htmlFor="quantity">
            Quantity:
          </label>

          <input
            id="quantity"
            type="number"
            required
            min={1}
            value={quantity}
            disabled={isSubmitting}
            onChange={(event) => {
              setQuantity(
                Number.parseInt(
                  event.target.value,
                  10
                )
              )
            }}
          />
        </div>

        <div className="form-group">
          <label htmlFor="account">
            Account:
          </label>

          <select
            id="account"
            value={account}
            onChange={handleAccountChange}
            disabled={isSubmitting}
          >
            {accountOptions.map((customer) => {
              const accountValue =
                customer.account ||
                customer.Account

              const balanceValue =
                customer.balance !== undefined
                  ? customer.balance
                  : customer.Balance

              return (
                <option
                  key={accountValue}
                  value={accountValue}
                >
                  {accountValue} (
                  Balance: {balanceValue} RSD)
                </option>
              )
            })}
          </select>
        </div>

        {purchaseStatus && (
          <div className="purchase-status">
            Status kupovine: {purchaseStatus}
          </div>
        )}

        <button
          type="submit"
          className="button-form"
          disabled={isSubmitting}
        >
          {isSubmitting
            ? 'Processing...'
            : 'Submit'}
        </button>
      </form>
    </>
  )
}

export default App