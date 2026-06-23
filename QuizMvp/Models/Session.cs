using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizMvp.Models
{
    public enum SessionStatus
    {
        Waiting,
        Active,
        Finished
    }

    public class Session
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Quiz")]
        public int QuizId { get; set; }
        public virtual Quiz Quiz { get; set; }

        [Required]
        public string JoinCode { get; set; } // Уникальный 6-значный код

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public SessionStatus Status { get; set; } = SessionStatus.Waiting;

        public int CurrentQuestionIndex { get; set; } = 0;

        public virtual ICollection<ParticipantAnswer> ParticipantAnswers { get; set; }
    }
}