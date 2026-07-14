namespace BackendEsame.Classi
{
    public class Corso
    {
        public int CorsoID { get; set; }
        public string Titolo { get; set; }
        public string? Descrizione { get; set; }
        public string Categoria { get; set; }
        public int DurataOre { get; set; }
        public bool Obbligatorio { get; set; }
        public bool Attivo { get; set; }
        
    }
}
