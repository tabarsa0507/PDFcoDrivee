using System.ComponentModel.DataAnnotations;

namespace PDFcoDrive.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "نام گروه")]
        public string Name { get; set; } = "";

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [StringLength(50)]
        public string Icon { get; set; } = "bi-people-fill";

        [StringLength(20)]
        public string Color { get; set; } = "#8b5cf6";

        // اعضای گروه (User IDs)
        public List<string> MemberIds { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}