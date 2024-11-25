namespace NocturnalBrews.Models
{
    public class ProductViewModel
    {
        public int? ProductId { get; set; }
        public string ProductName { get; set; }
        public string Categories { get; set; }
        public Dictionary<string, string> Prices { get; set; }
    }
}
