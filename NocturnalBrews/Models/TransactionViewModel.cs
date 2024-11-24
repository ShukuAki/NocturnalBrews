namespace NocturnalBrews.Models
{
    public class TransactionViewModel
    {
        public List<OrderProduct> Products { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMode { get; set; }
        public decimal Change { get; set; }
    }

    public class OrderProduct
    {
        public string ProductName { get; set; }
        public string Size { get; set; }
        public decimal Price { get; set; }
    }
}
