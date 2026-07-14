using System.ComponentModel.DataAnnotations;

namespace BackendEsame.Classi
{
    public class RequestRegister
    {
        [Required]
        public string Nome { get; set; }

        [Required]
        public string Cognome { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "La password non può essere vuota")]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }
    }
}
