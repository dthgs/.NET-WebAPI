using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
 
namespace MyBGList.DTO
{
    public class BoardGameDTO
    {
        [Required]
        public int Id { get; set; }
 
        public string? Name { get; set; }
 
        public int? Year { get; set; }
    }
}

// Making changes