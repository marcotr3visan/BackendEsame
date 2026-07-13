namespace BackendEsame.Classi
{
    public class RequestInserisci
    {
        public DateTime DataRichiesta { get; set; }
        public DateTime DataInizio { get; set; }
        public DateTime DataFine { get; set; }
        public int CategoriaID { get; set; }
        public string Motivazione { get; set; }
        public String Stato { get; set; }
        public int UtenteID { get; set; }
    }
}
