namespace CalculatorApp.Models
{
    public class CalculatorModel
    {
        public string Display { get; set; } = "0";
        public double Result { get; set; } = 0;
        public string Operation { get; set; } = string.Empty;
        public bool IsNewInput { get; set; } = true;
        public string CombinedDisplay { get; set; } = ""; // Ensure this is writable
    }
}