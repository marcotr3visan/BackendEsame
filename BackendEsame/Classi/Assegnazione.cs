namespace BackendEsame.Classi
{
    public class Assegnazione
    {
        public int AssegnazioneID { get; set; }
        public int CorsoID { get; set; }
        public int DipendenteID { get; set; }
        public DateOnly DataAssegnazione { get; set; }
        public DateOnly DataScadenza { get; set; }
        public string Stato { get; set; }
        public DateOnly? DataCompletamento { get; set; }
    }
}
