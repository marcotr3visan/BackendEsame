using System.ComponentModel.DataAnnotations;

namespace BackendEsame.Classi
{
    public class RequestInserisci
    {
        [Required]
        public string Titolo { get; set; }

        public string? Descrizione { get; set; }

        [Required]
        public string Categoria { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La durata deve essere maggiore di zero")]
        public int DurataOre { get; set; }

        public bool Obbligatorio { get; set; }

        public bool Attivo { get; set; }
    }
}
