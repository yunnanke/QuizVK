using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static System.Collections.Specialized.BitVector32;

namespace QuizMvp.Models
{
    public class Quiz
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public string Category { get; set; }

        public int TimeLimitPerQuestion { get; set; } = 30; // секунд

        public string Rules { get; set; }

        [ForeignKey("Creator")]
        public int CreatorId { get; set; }
        public virtual User Creator { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Question> Questions { get; set; }
        public virtual ICollection<Session> Sessions { get; set; }
    }
}