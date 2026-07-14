namespace BackendEsame.Classi
{
    public class RequestAssegnazione
    {
        public int CorsoID { get; set; }
        public int DipendenteID { get; set; }
        public DateOnly DataScadenza { get; set; }
    }
}
