using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BackendEsame.Controllers;

[Route("api/statistiche")]
[ApiController]
public class StatisticheController : ControllerBase
{
    private readonly ILogger<StatisticheController> _logger;
    private readonly string ConnectionString;

    public StatisticheController(ILogger<StatisticheController> logger, IConfiguration configuration)
    {
        _logger = logger;
        ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost\\SQLEXPRESS;Database=Esame;Trusted_Connection=True;TrustServerCertificate=True";
    }

    [Route("academy")]
    [HttpGet]
    [Authorize(Roles = "Referente")]
    public async Task<IActionResult> Academy(
        [FromQuery] string? mese = null,
        [FromQuery] string? categoria = null,
        [FromQuery] int? dipendenteID = null)
    {
        try
        {
            var results = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await using var cmd = conn.CreateCommand();

            var query = @"SELECT
                            CONVERT(varchar(7), a.DataAssegnazione, 120) AS Mese,
                            c.Categoria,
                            COUNT(*) AS NumeroAssegnazioni,
                            SUM(CASE WHEN a.Stato = 'Completato' THEN 1 ELSE 0 END) AS NumeroCompletamenti
                          FROM TAssegnazioni a
                          INNER JOIN TCorsi c ON a.CorsoID = c.CorsoID
                          WHERE 1=1";

            if (!string.IsNullOrEmpty(mese))
            {
                query += " AND CONVERT(varchar(7), a.DataAssegnazione, 120) = @Mese";
                cmd.Parameters.AddWithValue("@Mese", mese);
            }

            if (!string.IsNullOrEmpty(categoria))
            {
                query += " AND c.Categoria = @Categoria";
                cmd.Parameters.AddWithValue("@Categoria", categoria);
            }

            if (dipendenteID.HasValue)
            {
                query += " AND a.DipendenteID = @DipendenteID";
                cmd.Parameters.AddWithValue("@DipendenteID", dipendenteID.Value);
            }

            query += @" GROUP BY CONVERT(varchar(7), a.DataAssegnazione, 120), c.Categoria
                        ORDER BY Mese DESC, c.Categoria";

            cmd.CommandText = query;

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int numAssegnazioni = (int)reader["NumeroAssegnazioni"];
                int numCompletamenti = (int)reader["NumeroCompletamenti"];
                double percentuale = numAssegnazioni > 0 ? Math.Round((double)numCompletamenti / numAssegnazioni * 100, 2) : 0;

                results.Add(new
                {
                    Mese = reader["Mese"].ToString(),
                    Categoria = reader["Categoria"].ToString(),
                    NumeroAssegnazioni = numAssegnazioni,
                    NumeroCompletamenti = numCompletamenti,
                    PercentualeCompletamento = percentuale
                });
            }

            if (results.Count == 0)
                return NotFound("Nessun dato statistico trovato");

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il recupero delle statistiche");
            return StatusCode(500, "Errore interno del server");
        }
    }
}
