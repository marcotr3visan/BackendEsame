using System.ComponentModel.DataAnnotations;

namespace BackendEsame.Classi
{
    public class RequestLogin
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
