using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizMvp.Models
{
    public enum QuestionType
    {
        Text,
        Image
    }

    public class Question
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Quiz")]
        public int QuizId { get; set; }
        public virtual Quiz Quiz { get; set; }

        public QuestionType Type { get; set; } = QuestionType.Text;

        [Required]
        public string Text { get; set; }

        public string? ImageUrl { get; set; }  

        public bool IsMultipleChoice { get; set; } = false;

        public int Points { get; set; } = 10;

        public virtual ICollection<AnswerOption> Options { get; set; }
    }
}