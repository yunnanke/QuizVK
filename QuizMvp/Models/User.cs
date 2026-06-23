using System.ComponentModel.DataAnnotations;

namespace QuizMvp.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string Role { get; set; } = "Participant"; 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}