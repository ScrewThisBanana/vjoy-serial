using System.ComponentModel.DataAnnotations;

public class PortConfiguration
{    
    [Required]
    [RegularExpression(@"^COM\d+$")]
    public required string SerialPort { get; set; }
    [Required]
    public int BaudRate { get; set; }
}