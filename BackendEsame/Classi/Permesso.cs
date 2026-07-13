namespace BackendEsame.Classi
{
    public class Permesso
    {
        public int RichiestaID { get; set; }
        public DateTime DataRichiesta { get; set; }
        public DateTime DataInizio { get; set; }
        public DateTime DataFine { get; set; }
        public int CategoriaID { get; set; }
        public string Motivazione { get; set; }
        public string Stato { get; set; }
        public int UtenteID { get; set; }
        public DateTime? DataValutazione { get; set; }
        public int? UtenteValutazioneID { get; set; }
    }
}
