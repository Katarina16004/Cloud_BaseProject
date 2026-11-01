namespace ClassLibrary
{
    public class ConsistencyResult
    {
        public int BookId { get; set; }
        public string BookName { get; set; }
        public int Stock { get; set; }
        public int Reserved { get; set; }
        public int ReservationCount { get; set; }
        public int ReservedQuantitySum { get; set; }
        public int InitialStock { get; set; }
        public bool IsConsistent { get; set; }
    }
}