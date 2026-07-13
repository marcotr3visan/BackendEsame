using BackendEsame.Classi;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace BackendEsame.Controllers;

[Route("permessi")]
public class PermessiController : ControllerBase
{
    private readonly ILogger<PermessiController> _logger;
    private const string ConnectionString = "Server=localhost\\SQLEXPRESS;Database=Permessi;Trusted_Connection=True;TrustServerCertificate=True";

    public PermessiController(ILogger<PermessiController> logger)
    {
        _logger = logger;
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.Claims.FirstOrDefault(c => c.Type == "UserID")?.Value;
        return claim != null && int.TryParse(claim, out userId);
    }

    private string GetUserRole()
    {
        return User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
    }

    [Route("")]
    [HttpGet]
    [Authorize(Roles = "Responsabile,Dipendente")]
    public async Task<IActionResult> Richieste()
    {
        if (!TryGetUserId(out int userId))
            return Unauthorized("UserID mancante");

        var role = GetUserRole();

        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();
            if (role == "Responsabile")
            {
                cmd.CommandText = "SELECT RichiestaID, DataRichiesta, DataInizio, DataFine, Motivazione, Stato FROM TRichieste";
            }
            else
            {
                cmd.CommandText = "SELECT RichiestaID, DataRichiesta, DataInizio, DataFine, Motivazione, Stato FROM TRichieste WHERE UtenteID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);
            }

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    RichiestaID = reader["RichiestaID"],
                    DataRichiesta = reader["DataRichiesta"],
                    DataInizio = reader["DataInizio"],
                    DataFine = reader["DataFine"],
                    Motivazione = reader["Motivazione"],
                    Stato = reader["Stato"]
                });
            }

            if (results.Count == 0)
                return NotFound("Nessuna richiesta trovata");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'elenco delle richieste per user {UserId}", userId);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> RichiesteById([FromRoute] int id)
    {
        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT RichiestaID, DataRichiesta, DataInizio, DataFine, Motivazione, Stato FROM TRichieste WHERE RichiestaID = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    RichiestaID = reader["RichiestaID"],
                    DataRichiesta = reader["DataRichiesta"],
                    DataInizio = reader["DataInizio"],
                    DataFine = reader["DataFine"],
                    Motivazione = reader["Motivazione"],
                    Stato = reader["Stato"]
                });
            }

            if (results.Count == 0)
                return NotFound("Nessuna richiesta trovata");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la lettura della richiesta {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("")]
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> InserisciRichiesta([FromBody] RequestInserisci myRequestInserisci)
    {
        if (myRequestInserisci == null || !ModelState.IsValid)
            return BadRequest("Dati mancanti o non validi");

        if (!TryGetUserId(out int userId))
            return Unauthorized("UserID mancante");

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"INSERT INTO TRichieste (DataRichiesta, DataInizio, DataFine, CategoriaID, Motivazione, Stato, UtenteID)
                                VALUES (@DataRichiesta, @DataInizio, @DataFine, @CategoriaID, @Motivazione, @Stato, @UtenteID);";

            cmd.Parameters.AddWithValue("@DataRichiesta", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@DataInizio", myRequestInserisci.DataInizio);
            cmd.Parameters.AddWithValue("@DataFine", myRequestInserisci.DataFine);
            cmd.Parameters.AddWithValue("@CategoriaID", myRequestInserisci.CategoriaID);
            cmd.Parameters.AddWithValue("@Motivazione", myRequestInserisci.Motivazione ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Stato", "In attesa");
            cmd.Parameters.AddWithValue("@UtenteID", userId);

            await conn.OpenAsync();
            var righeAggiunte = await cmd.ExecuteNonQueryAsync();

            if (righeAggiunte > 0)
                return Ok(new { message = "Richiesta aggiunta" });

            _logger.LogWarning("Nessuna riga inserita per la richiesta di utente {UtenteID}", userId);
            return BadRequest("Errore durante l'inserimento");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'inserimento di una richiesta");
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpPut]
    [Authorize(Roles = "Dipendente,Responsabile")]
    public async Task<IActionResult> ModificaRichiesta(int id, [FromBody] RequestModifica myRequest)
    {
        if (myRequest == null || !ModelState.IsValid)
            return BadRequest("Dati mancanti o non validi");

        if (!TryGetUserId(out int userId))
            return Unauthorized("UserID mancante");

        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT UtenteID, Stato FROM TRichieste WHERE RichiestaID = @RichiestaID";
                checkCmd.Parameters.AddWithValue("@RichiestaID", id);
                await using var reader = await checkCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound($"Nessuna richiesta trovata con ID {id}");

                int ownerId = (int)reader["UtenteID"];
                string stato = reader["Stato"]?.ToString() ?? string.Empty;

                if (stato != "In attesa")
                    return BadRequest("La richiesta non può essere modificata, non è in stato 'In attesa'");

                if (role == "Dipendente" && ownerId != userId)
                    return Forbid();
            }

            var setClauses = new List<string>();
            await using var cmd = conn.CreateCommand();

            if (myRequest.DataInizio.HasValue)
            {
                setClauses.Add("DataInizio = @DataInizio");
                cmd.Parameters.AddWithValue("@DataInizio", myRequest.DataInizio.Value);
            }

            if (myRequest.DataFine.HasValue)
            {
                setClauses.Add("DataFine = @DataFine");
                cmd.Parameters.AddWithValue("@DataFine", myRequest.DataFine.Value);
            }

            if (myRequest.CategoriaID.HasValue)
            {
                setClauses.Add("CategoriaID = @CategoriaID");
                cmd.Parameters.AddWithValue("@CategoriaID", myRequest.CategoriaID.Value);
            }

            if (!string.IsNullOrEmpty(myRequest.Motivazione))
            {
                setClauses.Add("Motivazione = @Motivazione");
                cmd.Parameters.AddWithValue("@Motivazione", myRequest.Motivazione);
            }

            if (!string.IsNullOrEmpty(myRequest.Stato))
            {
                setClauses.Add("Stato = @Stato");
                cmd.Parameters.AddWithValue("@Stato", myRequest.Stato);
            }

            if (setClauses.Count == 0)
                return BadRequest("Nessun campo da aggiornare");

            cmd.CommandText = $"UPDATE TRichieste SET {string.Join(", ", setClauses)} WHERE RichiestaID = @RichiestaID";
            cmd.Parameters.AddWithValue("@RichiestaID", id);

            var righeAggiornate = await cmd.ExecuteNonQueryAsync();
            if (righeAggiornate == 0)
                return NotFound($"Nessuna richiesta trovata con ID {id}");

            return Ok(new { message = $"Richiesta {id} aggiornata con successo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'aggiornamento della richiesta {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}")]
    [HttpDelete]
    [Authorize(Roles = "Dipendente,Responsabile")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out int userId))
            return Unauthorized("UserID mancante");

        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT UtenteID, Stato FROM TRichieste WHERE RichiestaID = @RichiestaID";
                checkCmd.Parameters.AddWithValue("@RichiestaID", id);
                await using var reader = await checkCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound($"Nessuna richiesta trovata con ID {id}");

                int ownerId = (int)reader["UtenteID"];
                string stato = reader["Stato"]?.ToString() ?? string.Empty;

                if (stato != "In attesa")
                    return BadRequest("Non puoi eliminare richieste già approvate o rifiutate");

                if (role == "Dipendente" && ownerId != userId)
                    return Forbid();
            }

            await using (var deleteCmd = conn.CreateCommand())
            {
                deleteCmd.CommandText = "DELETE FROM TRichieste WHERE RichiestaID = @RichiestaID";
                deleteCmd.Parameters.AddWithValue("@RichiestaID", id);
                var righeEliminate = await deleteCmd.ExecuteNonQueryAsync();

                if (righeEliminate == 0)
                    return NotFound($"Nessuna richiesta trovata con ID {id}");

                return Ok(new { message = "Richiesta eliminata con successo" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'eliminazione della richiesta {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("da-approvare")]
    [HttpGet]
    [Authorize(Roles = "Responsabile")]
    public async Task<IActionResult> RichiesteDaApprovare()
    {
        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT UtenteID, RichiestaID, DataRichiesta, DataInizio, DataFine, Motivazione, Stato FROM TRichieste WHERE Stato = @Stato";
            cmd.Parameters.AddWithValue("@Stato", "In attesa");

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    UtenteID = reader["UtenteID"],
                    RichiestaID = reader["RichiestaID"],
                    DataRichiesta = reader["DataRichiesta"],
                    DataInizio = reader["DataInizio"],
                    DataFine = reader["DataFine"],
                    Motivazione = reader["Motivazione"],
                    Stato = reader["Stato"]
                });
            }

            if (results.Count == 0)
                return NotFound("Nessuna richiesta trovata");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'elenco delle richieste da approvare");
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}/approva")]
    [HttpPut]
    [Authorize(Roles = "Responsabile")]
    public async Task<IActionResult> ApprovaRichiesta([FromRoute] int id)
    {
        if (!TryGetUserId(out int userId))
            return Unauthorized("UserID mancante");
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "UPDATE TRichieste SET Stato = @Stato, DataValutazione = @DataValutazione, UtenteValutazioneID = @UtenteValutazioneID WHERE RichiestaID = @RichiestaID";
            cmd.Parameters.AddWithValue("@Stato", "Approvato");
            cmd.Parameters.AddWithValue("@DataValutazione", DateTime.Now);
            cmd.Parameters.AddWithValue("@UtenteValutazioneID", userId);
            cmd.Parameters.AddWithValue("@RichiestaID", id);

            await conn.OpenAsync();
            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessuna richiesta trovata con ID {id}");

            return Ok($"Richiesta {id} aggiornata con successo");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'approvazione della richiesta {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("{id}/rifiuta")]
    [HttpPut]
    [Authorize(Roles = "Responsabile")]
    public async Task<IActionResult> RifiutaRichiesta([FromRoute] int id)
    {
        if (!TryGetUserId(out int userId))
            return Unauthorized("UserID mancante");
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "UPDATE TRichieste SET Stato = @Stato, DataValutazione = @DataValutazione, UtenteValutazioneID = @UtenteValutazioneID WHERE RichiestaID = @RichiestaID";
            cmd.Parameters.AddWithValue("@Stato", "Rifiutato");
            cmd.Parameters.AddWithValue("@DataValutazione", DateTime.Now);
            cmd.Parameters.AddWithValue("@UtenteValutazioneID", userId);
            cmd.Parameters.AddWithValue("@RichiestaID", id);

            await conn.OpenAsync();
            var righeAggiornate = await cmd.ExecuteNonQueryAsync();

            if (righeAggiornate == 0)
                return NotFound($"Nessuna richiesta trovata con ID {id}");

            return Ok($"Richiesta {id} aggiornata con successo");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il rifiuto della richiesta {Id}", id);
            return StatusCode(500, "Errore interno del server");
        }
    }

    [Route("stats")]
    [HttpGet]
    [Authorize(Roles = "Responsabile")]
    public async Task<IActionResult> GetStatistiche()
    {
        var role = GetUserRole();

        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM VistaGiorni";

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    UtenteID = reader["UtenteID"],
                    Nome = reader["Nome"],
                    Cognome = reader["Cognome"],
                    Email = reader["Email"],
                    GiorniPermessoApprovati = reader["GiorniPermessoApprovati"]

                });
            }

            if (results.Count == 0)
                return NotFound("Nessuna richiesta trovata");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'elenco delle richieste");
            return StatusCode(500, "Errore interno del server");
        }
    }
}