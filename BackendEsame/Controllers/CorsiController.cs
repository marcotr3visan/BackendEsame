using BackendEsame.Classi;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace BackendEsame.Controllers;

[Route("api/corsi")]
[ApiController]
public class CorsiController : ControllerBase
{
    private readonly ILogger<CorsiController> _logger;
    private readonly string ConnectionString;

    public CorsiController(ILogger<CorsiController> logger, IConfiguration configuration)
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
    public async Task<IActionResult> Corsi(
        [FromQuery] string? categoria = null,
        [FromQuery] bool? attivo = null)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        var role = GetUserRole();

        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            string query;
            if (role == "Referente")
            {
                query = @"SELECT CorsoID, Titolo, Descrizione, Categoria, DurataOre, Obbligatorio, Attivo FROM TCorsi WHERE 1=1";
            }
            else
            {
                query = @"SELECT DISTINCT c.CorsoID, c.Titolo, c.Descrizione, c.Categoria, c.DurataOre, c.Obbligatorio, c.Attivo
                          FROM TAssegnazioni ac INNER JOIN TCorsi c ON ac.CorsoID = c.CorsoID
                          WHERE ac.DipendenteID = @UtenteID";

                cmd.Parameters.AddWithValue("@UtenteID", utenteID);
            }

            if (!string.IsNullOrEmpty(categoria))
            {
                query += " AND Categoria = @Categoria";
                cmd.Parameters.AddWithValue("@Categoria", categoria);
            }

            if (attivo.HasValue)
            {
                query += " AND Attivo = @Attivo";
                cmd.Parameters.AddWithValue("@Attivo", attivo.Value);
            }

            query += " ORDER BY Titolo";
            cmd.CommandText = query;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    CorsoID = reader["CorsoID"],
                    Titolo = reader["Titolo"],
                    Descrizione = reader["Descrizione"],
                    Categoria = reader["Categoria"],
                    DurataOre = reader["DurataOre"],
                    Obbligatorio = reader["Obbligatorio"],
                    Attivo = reader["Attivo"]
                });
            }

            if (results.Count == 0)
                return NotFound("Nessun corso trovato");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'elenco dei corsi per user {UtenteID}", utenteID);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpGet]
    [Authorize]
    public async Task<IActionResult>CorsiById([FromRoute] int id)
    {
        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CorsoID, Titolo, Descrizione, Categoria, DurataOre, Obbligatorio, Attivo FROM TCorsi WHERE CorsoID = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    CorsoID = reader["CorsoID"],
                    Titolo = reader["Titolo"],
                    Descrizione = reader["Descrizione"],
                    Categoria = reader["Categoria"],
                    DurataOre = reader["DurataOre"],
                    Obbligatorio = reader["Obbligatorio"],
                    Attivo = reader["Attivo"]
                });
            }

            if (results.Count == 0)
                return NotFound("Nessun corso trovata");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la lettura del corso {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("")]
    [HttpPost]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> InserisciCorso([FromBody] RequestInserisci myRequestInserisci)
    {
        if (myRequestInserisci == null || !ModelState.IsValid)
            return BadRequest("Dati mancanti o non validi");

        if (myRequestInserisci.DurataOre <= 0)
            return BadRequest("La durata prevista deve essere maggiore di zero");

        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"INSERT INTO TCorsi (
                        Titolo, Descrizione, Categoria, DurataOre, Obbligatorio, Attivo)
                        VALUES (@Titolo, @Descrizione, @Categoria, @DurataOre, @Obbligatorio, @Attivo)";

            cmd.Parameters.AddWithValue("@Titolo", myRequestInserisci.Titolo);
            cmd.Parameters.AddWithValue("@Descrizione", (object?)myRequestInserisci.Descrizione ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Categoria", myRequestInserisci.Categoria);
            cmd.Parameters.AddWithValue("@DurataOre", myRequestInserisci.DurataOre);
            cmd.Parameters.AddWithValue("@Obbligatorio", myRequestInserisci.Obbligatorio);
            cmd.Parameters.AddWithValue("@Attivo", myRequestInserisci.Attivo);

            await conn.OpenAsync();
            var righeAggiunte = await cmd.ExecuteNonQueryAsync();

            if (righeAggiunte > 0)
                return Ok(new { message = "Corso aggiunto" });

            _logger.LogWarning("Nessuna riga inserita per il corso di utente {UtenteID}", utenteID);
            return BadRequest("Errore durante l'inserimento");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'inserimento del corso");
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpPut]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> ModificaCorso(int id, [FromBody] RequestInserisci myRequest)
    {
        if (myRequest == null || !ModelState.IsValid)
            return BadRequest("Dati mancanti o non validi");

        if (myRequest.DurataOre <= 0)
            return BadRequest("La durata prevista deve essere maggiore di zero");

        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"UPDATE TCorsi SET 
                        Titolo = @Titolo, 
                        Descrizione = @Descrizione, 
                        Categoria = @Categoria, 
                        DurataOre = @DurataOre, 
                        Obbligatorio = @Obbligatorio, 
                        Attivo = @Attivo
                        WHERE CorsoID = @CorsoID";

            cmd.Parameters.AddWithValue("@CorsoID", id);
            cmd.Parameters.AddWithValue("@Titolo", myRequest.Titolo);
            cmd.Parameters.AddWithValue("@Descrizione", (object?)myRequest.Descrizione ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Categoria", myRequest.Categoria);
            cmd.Parameters.AddWithValue("@DurataOre", myRequest.DurataOre);
            cmd.Parameters.AddWithValue("@Obbligatorio", myRequest.Obbligatorio);
            cmd.Parameters.AddWithValue("@Attivo", myRequest.Attivo);

            await conn.OpenAsync();
            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessun corso trovato con ID {id}");

            return Ok(new { message = $"Corso {id} aggiornato con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'aggiornamento del corso {Id}", id);
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

            await using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM TAssegnazioni WHERE CorsoID = @CorsoID";
                checkCmd.Parameters.AddWithValue("@CorsoID", id);
                var count = (int)await checkCmd.ExecuteScalarAsync();

                if (count > 0)
                    return BadRequest("Impossibile eliminare il corso: ha assegnazioni collegate");
            }

            await using (var deleteCmd = conn.CreateCommand())
            {
                deleteCmd.CommandText = "DELETE FROM TCorsi WHERE CorsoID = @CorsoID";
                deleteCmd.Parameters.AddWithValue("@CorsoID", id);
                var righeEliminate = await deleteCmd.ExecuteNonQueryAsync();

                if (righeEliminate == 0)
                    return NotFound($"Nessun corso trovato con ID {id}");

                return Ok(new { message = "Corso eliminato con successo" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'eliminazione del corso {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}/disattiva")]
    [HttpPut]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> DisattivaCorso(int id)
    {
        if (!TryGetUserId(out int utenteID))
            return Unauthorized("UtenteID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "UPDATE TCorsi SET Attivo = 0 WHERE CorsoID = @CorsoID";
            cmd.Parameters.AddWithValue("@CorsoID", id);

            await conn.OpenAsync();
            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessun corso trovato con ID {id}");

            return Ok(new { message = $"Corso {id} disattivato con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la disattivazione del corso {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

}