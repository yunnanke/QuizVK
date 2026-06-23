using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizMvp.Models
{
    public class AnswerOption
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Question")]
        public int QuestionId { get; set; }
        public virtual Question Question { get; set; }

        [Required]
        public string Text { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsCorrect { get; set; } = false;
    }
}