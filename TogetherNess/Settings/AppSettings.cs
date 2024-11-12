using System.ComponentModel.DataAnnotations;

namespace TogetherNess.Settings;

public class AppSettings
{
    [Required]
    public string MappersDirectory { get; set; }
}