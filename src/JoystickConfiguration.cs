using System.ComponentModel.DataAnnotations;

public class JoystickConfiguration
{
    [Required]
    [Range(1, 16)]
    public uint JoystickId { get; set; }
}
