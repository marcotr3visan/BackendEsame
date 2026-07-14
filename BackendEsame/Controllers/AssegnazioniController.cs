using BackendEsame.Classi;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BackendEsame.Controllers;

[Route("api/assegnazioni")]
[ApiController]
public class AssegnazioniController : ControllerBase
{
    private readonly ILogger<AssegnazioniController> _logger;
    private readonly string ConnectionString;

    public AssegnazioniController(ILogger<AssegnazioniController> logger, IConfiguration configuration)
    {
        _logger = logger;
        ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost\\SQLEXPRESS;Database=Esame;Trusted_Connection=True;TrustServerCertificate=True";
    }

    private bool TryGetUserId(out int utenteID)
    {
        utenteID = 0;
        var claim = User.Claims.FirstOrDefault(c => c.Type == "UtenteID")?.Value;
        return claim != null && int.TryParse(claim, out utenteID);
    }

    private string GetUserRole()
    {
        return User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
    }

    [Route("")]
    [HttpGet]
    [Authorize(Roles = "Referente,Dipendente")]
    public async Task<IActionResult> Assegnazioni(
        [FromQuery] string? stato = null,
        [FromQuery] string? categoria = null,
        [FromQuery] int? corsoID = null,
        [FromQuery] int? dipendenteID = null)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using (var aggiornaCmd = conn.CreateCommand())
            {
                aggiornaCmd.CommandText = @"UPDATE TAssegnazioni SET Stato = 'Scaduto'
                                            WHERE Stato = 'Assegnato' AND DataScadenza < @Oggi";
                aggiornaCmd.Parameters.AddWithValue("@Oggi", DateOnly.FromDateTime(DateTime.Now));
                await aggiornaCmd.ExecuteNonQueryAsync();
            }

            var results = new List<object>();
            await using var cmd = conn.CreateCommand();

            var query = @"SELECT a.AssegnazioneID, a.CorsoID, c.Titolo, c.Categoria,
                                 a.DipendenteID, u.Nome, u.Cognome,
                                 a.DataAssegnazione, a.DataScadenza, a.Stato, a.DataCompletamento
                          FROM TAssegnazioni a
                          INNER JOIN TCorsi c ON a.CorsoID = c.CorsoID
                          INNER JOIN TUsers u ON a.DipendenteID = u.UtenteID
                          WHERE 1=1";

            if (role != "Referente")
            {
                query += " AND a.DipendenteID = @UtenteID AND a.Stato <> 'Annullato'";
                cmd.Parameters.AddWithValue("@UtenteID", utenteID);
            }
            else if (dipendenteID.HasValue)
            {
                query += " AND a.DipendenteID = @DipendenteID";
                cmd.Parameters.AddWithValue("@DipendenteID", dipendenteID.Value);
            }

            if (!string.IsNullOrEmpty(stato))
            {
                query += " AND a.Stato = @Stato";
                cmd.Parameters.AddWithValue("@Stato", stato);
            }

            if (!string.IsNullOrEmpty(categoria))
            {
                query += " AND c.Categoria = @Categoria";
                cmd.Parameters.AddWithValue("@Categoria", categoria);
            }

            if (corsoID.HasValue)
            {
                query += " AND a.CorsoID = @CorsoID";
                cmd.Parameters.AddWithValue("@CorsoID", corsoID.Value);
            }

            query += " ORDER BY a.DataAssegnazione DESC";

            cmd.CommandText = query;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    AssegnazioneID = reader["AssegnazioneID"],
                    CorsoID = reader["CorsoID"],
                    Titolo = reader["Titolo"],
                    Categoria = reader["Categoria"],
                    DipendenteID = reader["DipendenteID"],
                    Nome = reader["Nome"],
                    Cognome = reader["Cognome"],
                    DataAssegnazione = reader["DataAssegnazione"],
                    DataScadenza = reader["DataScadenza"],
                    Stato = reader["Stato"],
                    DataCompletamento = reader["DataCompletamento"]
                });
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'elenco delle assegnazioni");
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> AssegnazioneById(int id)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT a.AssegnazioneID, a.CorsoID, c.Titolo, c.Categoria,
                                       a.DipendenteID, u.Nome, u.Cognome,
                                       a.DataAssegnazione, a.DataScadenza, a.Stato, a.DataCompletamento
                                FROM TAssegnazioni a
                                INNER JOIN TCorsi c ON a.CorsoID = c.CorsoID
                                INNER JOIN TUsers u ON a.DipendenteID = u.UtenteID
                                WHERE a.AssegnazioneID = @Id";

            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return NotFound("Nessuna assegnazione trovata");

            int dipendenteID = (int)reader["DipendenteID"];

            if (role != "Referente" && dipendenteID != utenteID)
                return Forbid();

            var result = new
            {
                AssegnazioneID = reader["AssegnazioneID"],
                CorsoID = reader["CorsoID"],
                Titolo = reader["Titolo"],
                Categoria = reader["Categoria"],
                DipendenteID = dipendenteID,
                Nome = reader["Nome"],
                Cognome = reader["Cognome"],
                DataAssegnazione = reader["DataAssegnazione"],
                DataScadenza = reader["DataScadenza"],
                Stato = reader["Stato"],
                DataCompletamento = reader["DataCompletamento"]
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la lettura dell'assegnazione {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("")]
    [HttpPost]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> InserisciAssegnazione([FromBody] RequestAssegnazione myRequest)
    {
        if (myRequest == null || !ModelState.IsValid)
            return BadRequest("Dati mancanti o non validi");

        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            using (var checkCorso = new SqlCommand("SELECT Attivo FROM TCorsi WHERE CorsoID = @CorsoID", conn))
            {
                checkCorso.Parameters.AddWithValue("@CorsoID", myRequest.CorsoID);
                var corso = await checkCorso.ExecuteScalarAsync();
                if (corso == null)
                    return BadRequest("Corso non trovato");
                if (!(bool)corso)
                    return BadRequest("Il corso non è attivo");
            }

            using (var checkDipendente = new SqlCommand("SELECT COUNT(*) FROM TUsers WHERE UtenteID = @DipendenteID AND Ruolo = 'Dipendente'", conn))
            {
                checkDipendente.Parameters.AddWithValue("@DipendenteID", myRequest.DipendenteID);
                var exists = (int)await checkDipendente.ExecuteScalarAsync();
                if (exists == 0)
                    return BadRequest("Dipendente non trovato");
            }

            using (var checkDup = new SqlCommand(
                "SELECT COUNT(*) FROM TAssegnazioni WHERE CorsoID = @CorsoID AND DipendenteID = @DipendenteID AND Stato <> 'Annullato'", conn))
            {
                checkDup.Parameters.AddWithValue("@CorsoID", myRequest.CorsoID);
                checkDup.Parameters.AddWithValue("@DipendenteID", myRequest.DipendenteID);
                var dup = (int)await checkDup.ExecuteScalarAsync();
                if (dup > 0)
                    return BadRequest("Questo dipendente ha già un'assegnazione per questo corso");
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            if (myRequest.DataScadenza < today)
                return BadRequest("La data di scadenza non può essere precedente alla data di assegnazione");

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO TAssegnazioni (CorsoID, DipendenteID, DataAssegnazione, DataScadenza, Stato)
                                VALUES (@CorsoID, @DipendenteID, @DataAssegnazione, @DataScadenza, @Stato)";

            cmd.Parameters.AddWithValue("@CorsoID", myRequest.CorsoID);
            cmd.Parameters.AddWithValue("@DipendenteID", myRequest.DipendenteID);
            cmd.Parameters.AddWithValue("@DataAssegnazione", today);
            cmd.Parameters.AddWithValue("@DataScadenza", myRequest.DataScadenza);
            cmd.Parameters.AddWithValue("@Stato", "Assegnato");

            var righeAggiunte = await cmd.ExecuteNonQueryAsync();

            if (righeAggiunte > 0)
                return Ok(new { message = "Assegnazione creata con successo" });

            return BadRequest("Errore durante la creazione dell'assegnazione");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la creazione dell'assegnazione");
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpPut]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> ModificaAssegnazione(int id, [FromBody] RequestAssegnazione myRequest)
    {
        if (myRequest == null || !ModelState.IsValid)
            return BadRequest("Dati mancanti o non validi");

        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            DateOnly dataAssegnazione;

            using (var checkCmd = new SqlCommand("SELECT Stato, DataAssegnazione FROM TAssegnazioni WHERE AssegnazioneID = @Id", conn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                await using var reader = await checkCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound($"Nessuna assegnazione trovata con ID {id}");

                var stato = reader["Stato"]?.ToString() ?? string.Empty;
                if (stato != "Assegnato")
                    return BadRequest("Puoi modificare solo assegnazioni in stato 'Assegnato'");

                dataAssegnazione = (DateOnly)reader["DataAssegnazione"];
            }

            if (myRequest.DataScadenza < dataAssegnazione)
                return BadRequest("La data di scadenza non può essere precedente alla data di assegnazione");

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE TAssegnazioni SET DataScadenza = @DataScadenza WHERE AssegnazioneID = @Id";

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DataScadenza", myRequest.DataScadenza);

            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessuna assegnazione trovata con ID {id}");

            return Ok(new { message = $"Assegnazione {id} aggiornata con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'aggiornamento dell'assegnazione {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpDelete]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            using (var checkCmd = new SqlCommand("SELECT Stato FROM TAssegnazioni WHERE AssegnazioneID = @Id", conn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                var stato = await checkCmd.ExecuteScalarAsync();
                if (stato == null)
                    return NotFound($"Nessuna assegnazione trovata con ID {id}");
                if (stato.ToString() != "Assegnato")
                    return BadRequest("Puoi eliminare solo assegnazioni in stato 'Assegnato'");
            }

            await using var deleteCmd = conn.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM TAssegnazioni WHERE AssegnazioneID = @Id";
            deleteCmd.Parameters.AddWithValue("@Id", id);
            var righeEliminate = await deleteCmd.ExecuteNonQueryAsync();

            if (righeEliminate == 0)
                return NotFound($"Nessuna assegnazione trovata con ID {id}");

            return Ok(new { message = "Assegnazione eliminata con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'eliminazione dell'assegnazione {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}/completa")]
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> CompletaAssegnazione(int id)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            string stato;
            int dipendenteID;

            using (var checkCmd = new SqlCommand("SELECT Stato, DipendenteID FROM TAssegnazioni WHERE AssegnazioneID = @Id", conn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                await using var reader = await checkCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound($"Nessuna assegnazione trovata con ID {id}");

                stato = reader["Stato"]?.ToString() ?? string.Empty;
                dipendenteID = (int)reader["DipendenteID"];
            }

            if (stato != "Assegnato")
                return BadRequest("Solo le assegnazioni in stato 'Assegnato' possono essere completate");

            if (role == "Dipendente" && dipendenteID != utenteID)
                return Forbid();

            var today = DateOnly.FromDateTime(DateTime.Now);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE TAssegnazioni SET
                                Stato = 'Completato',
                                DataCompletamento = @DataCompletamento
                                WHERE AssegnazioneID = @Id";

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DataCompletamento", today);

            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessuna assegnazione trovata con ID {id}");

            return Ok(new { message = "Assegnazione completata con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il completamento dell'assegnazione {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}/annulla")]
    [HttpPut]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> AnnullaAssegnazione(int id)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            using (var checkCmd = new SqlCommand("SELECT Stato FROM TAssegnazioni WHERE AssegnazioneID = @Id", conn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                var stato = await checkCmd.ExecuteScalarAsync();
                if (stato == null)
                    return NotFound($"Nessuna assegnazione trovata con ID {id}");
                if (stato.ToString() != "Assegnato")
                    return BadRequest("Solo le assegnazioni in stato 'Assegnato' possono essere annullate");
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE TAssegnazioni SET Stato = 'Annullato' WHERE AssegnazioneID = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessuna assegnazione trovata con ID {id}");

            return Ok(new { message = "Assegnazione annullata con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'annullamento dell'assegnazione {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }
}
