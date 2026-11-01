import { useState, useEffect } from 'react'
import bookLogo from '/logo.png'
import './App.css'
import axios from 'axios'

function App() {
  const [books, setBooks] = useState([])
  const [accountOptions, setAccountOptions] = useState([])

  const [selectedBookName, setSelectedBookName] = useState('')
  const [author, setAuthor] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [account, setAccount] = useState('')
  const [userId, setUserId] = useState('')

  useEffect(() => {
    axios.get(
      'http://localhost:8939/api/BackendSF/books'
    )
      .then((response) => {
        setBooks(response.data)

        if (response.data.length > 0) {
          setSelectedBookName(response.data[0].name)
          setAuthor(response.data[0].author)
        }
      })
      .catch((err) => {
        console.error('Greška pri učitavanju knjiga:', err)
      })

    axios.get(
      'http://localhost:8939/api/BackendSF/accounts'
    )
      .then((response) => {
        setAccountOptions(response.data)

        if (response.data.length > 0) {
          const firstAcc = response.data[0]
          const firstAccount =
            firstAcc.account || firstAcc.Account

          setAccount(firstAccount)
          setUserId(firstAccount)
        }
      })
      .catch((err) => {
        console.error('Greška pri učitavanju naloga:', err)
      })
  }, [])

  const handleBookChange = (e) => {
    const bookName = e.target.value
    setSelectedBookName(bookName)

    const foundBook = books.find(
      (book) => book.name === bookName
    )

    if (foundBook) {
      setAuthor(foundBook.author)
    }
  }

  const handleAccountChange = (e) => {
    const selectedAccount = e.target.value
    setAccount(selectedAccount)
    setUserId(selectedAccount)
  }

  const handleSubmit = async (e) => {
    e.preventDefault()

    const selectedBook = books.find(
      (book) => book.name === selectedBookName
    )

    if (!selectedBook) {
      alert('Book not found')
      return
    }

    try {
      const response = await axios.post(
        'http://localhost:8939/api/BackendSF/purchase',
        {
          bookId: selectedBook.bookId,
          name: selectedBook.name,
          author: selectedBook.author,
          quantity,
          account,
          userId
        }
      )

      if (response.data.isValid === false) {
        alert(
          'Purchase failed! ' + response.data.message
        )
        return
      }

      alert('Purchase successful!')
    } catch (err) {
      console.error(err)
      alert('Server error')
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

      <form className="book-form" onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="name">Book name:</label>

          <select
            id="name"
            value={selectedBookName}
            onChange={handleBookChange}
          >
            {books.map((book) => (
              <option
                key={book.bookId}
                value={book.name}
              >
                {book.name} (Price: {book.price} RSD,
                Stock: {book.quantity})
              </option>
            ))}
          </select>
        </div>

        <div className="form-group">
          <label htmlFor="author">Author:</label>

          <input
            id="author"
            type="text"
            disabled
            value={author}
          />
        </div>

        <div className="form-group">
          <label htmlFor="quantity">Quantity:</label>

          <input
            id="quantity"
            type="number"
            required
            min={1}
            value={quantity}
            onChange={(e) => setQuantity(
              parseInt(e.target.value, 10)
            )}
          />
        </div>

        <div className="form-group">
          <label htmlFor="account">Account:</label>

          <select
            id="account"
            value={account}
            onChange={handleAccountChange}
          >
            {accountOptions.map((acc) => {
              const accountValue =
                acc.account || acc.Account

              const balanceValue =
                acc.balance !== undefined
                  ? acc.balance
                  : acc.Balance

              return (
                <option
                  key={accountValue}
                  value={accountValue}
                >
                  {accountValue} (Balance: {balanceValue} RSD)
                </option>
              )
            })}
          </select>
        </div>

        <button type="submit" className="button-form">
          Submit
        </button>
      </form>
    </>
  )
}

export default App