using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BackendEsame.Classi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace BackendEsame.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly string jwtSecret;
        private readonly string connectionString;

        public UserController(IConfiguration configuration)
        {
            jwtSecret = configuration["Jwt:Secret"] ?? "questaèunachiavesupersegretaperjwt123!";
            connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost\\SQLEXPRESS;Database=Esame;Trusted_Connection=True;TrustServerCertificate=True";
        }

        [Route("register")]
        [HttpPost]
        public IActionResult Register([FromBody] RequestRegister myRequestRegister)
        {
            if (myRequestRegister == null)
                return BadRequest("Dati mancanti");

            try
            {
                using (SqlConnection mysqlConnection = new SqlConnection(connectionString))
                {
                    mysqlConnection.Open();

                    using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM TUsers WHERE Email = @Email", mysqlConnection))
                    {
                        checkCmd.Parameters.AddWithValue("@Email", myRequestRegister.Email);
                        var exists = checkCmd.ExecuteScalar();
                        if (exists != null && (int)exists > 0)
                            return BadRequest("Email già registrata");
                    }

                    using (var mySqlCommand = new SqlCommand())
                    {
                        mySqlCommand.Connection = mysqlConnection;
                        mySqlCommand.Parameters.AddWithValue("@Nome", myRequestRegister.Nome);
                        mySqlCommand.Parameters.AddWithValue("@Cognome", myRequestRegister.Cognome);
                        mySqlCommand.Parameters.AddWithValue("@Email", myRequestRegister.Email);
                        string hashedPW = BCrypt.Net.BCrypt.HashPassword(myRequestRegister.Password);
                        mySqlCommand.Parameters.AddWithValue("@PasswordHash", hashedPW);
                        mySqlCommand.Parameters.AddWithValue("@Ruolo", myRequestRegister.Role);

                        mySqlCommand.CommandText = "INSERT INTO TUsers (Nome, Cognome, Email, PasswordHash, Ruolo)" +
                            "VALUES (@Nome, @Cognome, @Email, @PasswordHash, @Ruolo);";

                        int righeAggiunte = mySqlCommand.ExecuteNonQuery();

                        if (righeAggiunte > 0)
                            return Ok(new { message = "Registrazione avvenuta" });
                        else
                            return BadRequest("Errore durante la registrazione");
                    }
                }
            }
            catch
            {
                return StatusCode(500, "Errore interno del server");
            }
        }

        [Route("login")]
        [HttpPost]
        public IActionResult Login([FromBody] RequestLogin myRequestLogin)
        {
            if (myRequestLogin == null)
                return BadRequest("Dati mancanti");

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand cmd = new SqlCommand("SELECT * FROM TUsers WHERE Email = @Email", connection))
                    {
                        cmd.Parameters.AddWithValue("@Email", myRequestLogin.Email);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return BadRequest("Email o password non validi");

                            string hashedPasswordFromDb = reader["PasswordHash"]?.ToString() ?? string.Empty;

                            bool passwordCorretta = BCrypt.Net.BCrypt.Verify(myRequestLogin.Password, hashedPasswordFromDb);

                            if (!passwordCorretta)
                                return BadRequest("Email o password non validi");

                            int utenteID = (int)reader["UtenteID"];
                            string role = reader["Ruolo"]?.ToString() ?? string.Empty;

                            reader.Close();

                            var claims = new[]
                            {
                                new Claim("UtenteID", utenteID.ToString()),
                                new Claim(ClaimTypes.Role, role),
                                new Claim(JwtRegisteredClaimNames.Email, myRequestLogin.Email)
                            };

                            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
                            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                            var token = new JwtSecurityToken(
                                claims: claims,
                                expires: DateTime.Now.AddHours(1),
                                signingCredentials: creds
                            );

                            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                            return Ok(new
                            {
                                message = "Login avvenuto",
                                token = tokenString,
                                user = new
                                {
                                    Id = utenteID,
                                    Email = myRequestLogin.Email,
                                    Role = role
                                }
                            });
                        }
                    }
                }
            }
            catch
            {
                return StatusCode(500, "Errore interno del server");
            }
        }

        [Route("dipendenti")]
        [HttpGet]
        [Authorize(Roles = "Referente")]
        public IActionResult ElencoDipendenti()
        {
            try
            {
                var results = new List<object>();
                using (SqlConnection conn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand("SELECT UtenteID, Nome, Cognome, Email FROM TUsers WHERE Ruolo = 'Dipendente' ORDER BY Nome, Cognome", conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new
                            {
                                UtenteID = reader["UtenteID"],
                                Nome = reader["Nome"],
                                Cognome = reader["Cognome"],
                                Email = reader["Email"]
                            });
                        }
                    }
                }

                return Ok(results);
            }
            catch
            {
                return StatusCode(500, "Errore interno del server");
            }
        }

    }
}

