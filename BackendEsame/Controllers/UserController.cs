using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BackendEsame.Classi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace BackendEsame.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly string jwtSecret = "questaèunachiavesupersegretaperjwt123!";

        [Route("register")]
        [HttpPost]
        public IActionResult Register(RequestRegister myRequestRegister)
        {
            SqlConnection? mysqlConnection = null;
            string connectionString = "Server=localhost\\SQLEXPRESS;Database=Esame;Trusted_Connection=True;TrustServerCertificate=True";
            //string connectionString = "workstation id=CorriereEspresso.mssql.somee.com;packet size=4096;user id=marcotrevi_SQLLogin_1;pwd=fod9aqvbbq;data source=CorriereEspresso.mssql.somee.com;persist security info=False;initial catalog=CorriereEspresso;TrustServerCertificate=True";

            mysqlConnection = new SqlConnection(connectionString);

            SqlCommand mySqlCommand = new SqlCommand();
            mySqlCommand.Connection = mysqlConnection;

            mysqlConnection.Open();

            var checkCmd = new SqlCommand("SELECT COUNT(*) FROM TUsers WHERE Email = @Email", mysqlConnection);
            checkCmd.Parameters.AddWithValue("@Email", myRequestRegister.Email);
            if ((int)checkCmd.ExecuteScalar() > 0)
                return BadRequest("Email già registrata");

            mySqlCommand.Parameters.AddWithValue("@Nome", myRequestRegister.Nome);
            mySqlCommand.Parameters.AddWithValue("@Cognome", myRequestRegister.Cognome);
            mySqlCommand.Parameters.AddWithValue("@Email", myRequestRegister.Email);
            string hashedPW = BCrypt.Net.BCrypt.HashPassword(myRequestRegister.Password);
            mySqlCommand.Parameters.AddWithValue("@PasswordHash", hashedPW);
            mySqlCommand.Parameters.AddWithValue("@Role", myRequestRegister.Role);
    
            mySqlCommand.CommandText = "INSERT INTO TUsers (Nome, Cognome, Email, PasswordHash, Ruolo)" +
                "VALUES (@Nome, @Cognome, @Email, @PasswordHash, @Role);";

            int righeAggiunte = mySqlCommand.ExecuteNonQuery();

            if (righeAggiunte > 0)
                return Ok(new { message = "Registrazione avvenuta" });
            else
                return BadRequest("Errore");
        }

        [Route("login")]
        [HttpPost]
        public IActionResult Login(RequestLogin myRequestLogin)
        {
            string connectionString = "Server=localhost\\SQLEXPRESS;Database=Esame;Trusted_Connection=True;TrustServerCertificate=True";
            //string connectionString = "workstation id=CorriereEspresso.mssql.somee.com;packet size=4096;user id=marcotrevi_SQLLogin_1;pwd=fod9aqvbbq;data source=CorriereEspresso.mssql.somee.com;persist security info=False;initial catalog=CorriereEspresso;TrustServerCertificate=True";


            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand cmd = new SqlCommand("SELECT * FROM TUsers WHERE Email = @Email", connection);
                cmd.Parameters.AddWithValue("@Email", myRequestLogin.Email);

                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return BadRequest("Email o password non validi");

                string hashedPasswordFromDb = reader["PasswordHash"].ToString();

                bool passwordCorretta = BCrypt.Net.BCrypt.Verify(myRequestLogin.Password, hashedPasswordFromDb);

                if (!passwordCorretta)
                    return BadRequest("Email o password non validi");

                int utenteID = (int)reader["UID"];

                reader.Close();

                // Creazione JWT
                var claims = new[]
                {
                        new Claim("UtenteUD", utenteID.ToString()),
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
                    }
                });
            }
        }

    }
}

