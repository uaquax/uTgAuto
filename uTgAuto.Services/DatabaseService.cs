using Newtonsoft.Json;
using System.Data.SQLite;
using uTgAuto.Services.Models;

namespace uTgAuto.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            _connectionString = $"Data Source=User.db;Version=3;";
            initialize();
        }

        private void initialize()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS User (
                            ID INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatID INTEGER,
                            ApiID TEXT,
                            ApiHash TEXT,
                            Phone TEXT,
                            Password TEXT,
                            Messages JSON,
                            Code INTEGER,
                            Credits INTEGER,
                            State INTEGER
                        );";

                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                resetState();
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.initialize: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        public void resetState()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string updateQuery = @"
                        UPDATE User
                        SET State = @state;";

                    int newStateValue = (int)UserState.SignInTelegramService;

                    using (var command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@state", newStateValue);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.resetState: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        public List<User> GetUsers()
        {
            List<User> userList = new List<User>();

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string selectQuery = "SELECT * FROM User;";

                    using (var command = new SQLiteCommand(selectQuery, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                User user = new User
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    ChatID = Convert.ToInt64(reader["ChatID"]),
                                    ApiID = Convert.ToInt32(reader["ApiID"]),
                                    ApiHash = Convert.ToString(reader["ApiHash"]) ?? string.Empty,
                                    Phone = Convert.ToString(reader["Phone"]) ?? string.Empty,
                                    Password = Convert.ToString(reader["Password"]) ?? string.Empty,
                                    Code = Convert.ToString(reader["Code"]) ?? string.Empty,
                                    Credits = Convert.ToInt32(reader["Credits"]),
                                    State = (UserState)Convert.ToInt32(reader["State"]),
                                    Messages = JsonConvert.DeserializeObject<List<Message>>((string)reader["Messages"])!
                                };

                                userList.Add(user);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.GetUsers: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }

            return userList;
        }

        public User GetUserByChatID(long chatID)
        {
            User? user = null;

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string selectQuery = "SELECT * FROM User WHERE ChatID = @ChatID;";

                    using (var command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ChatID", chatID);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                user = new User
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    ChatID = Convert.ToInt64(reader["ChatID"]),
                                    ApiID = Convert.ToInt32(reader["ApiID"]),
                                    ApiHash = Convert.ToString(reader["ApiHash"]) ?? string.Empty,
                                    Phone = Convert.ToString(reader["Phone"]) ?? string.Empty,
                                    Password = Convert.ToString(reader["Password"]) ?? string.Empty,
                                    Code = Convert.ToString(reader["Code"]) ?? string.Empty,
                                    Credits = Convert.ToInt32(reader["Credits"]),
                                    State = (UserState)Convert.ToInt32(reader["State"]),
                                    Messages = JsonConvert.DeserializeObject<List<Message>>((string)reader["Messages"])!
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.GetUserByChatID: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }

            return user!;
        }


        public void AddUser(User user)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string insertQuery = @"
                INSERT INTO User (ChatID, ApiID, ApiHash, Phone, Password, Code, Credits, State, Messages)
                VALUES (@ChatID, @ApiID, @ApiHash, @Phone, @Password, @Code, @Credits, @State, @Messages);";

                    using (var command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ChatID", user.ChatID);
                        command.Parameters.AddWithValue("@ApiID", user.ApiID);
                        command.Parameters.AddWithValue("@ApiHash", user.ApiHash);
                        command.Parameters.AddWithValue("@Phone", user.Phone);
                        command.Parameters.AddWithValue("@Password", user.Password);
                        command.Parameters.AddWithValue("@Code", user.Code);
                        command.Parameters.AddWithValue("@Credits", user.Credits);
                        command.Parameters.AddWithValue("@State", (int)user.State);
                        command.Parameters.AddWithValue("@Messages", JsonConvert.SerializeObject(user.Messages));

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.AddUser: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        public void DeleteUser(long chatId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string deleteQuery = "DELETE FROM User WHERE ChatID = @chatid;";

                    using (var command = new SQLiteCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@chatid", chatId);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.DeleteUser: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }


        public void UpdateUser(User user)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string updateQuery = @"
                        UPDATE User
                        SET ChatID = @chatID,
                            ApiID = @apiID,
                            ApiHash = @apiHash,
                            Phone = @phone,
                            Password = @password,
                            Credits = @credits,
                            State = @state,
                            Messages = @messages
                        WHERE ChatID = @chatID;";

                    using (var command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@chatID", user.ChatID);
                        command.Parameters.AddWithValue("@apiID", user.ApiID);
                        command.Parameters.AddWithValue("@apiHash", user.ApiHash);
                        command.Parameters.AddWithValue("@phone", user.Phone);
                        command.Parameters.AddWithValue("@password", user.Password);
                        command.Parameters.AddWithValue("@credits", user.Credits);
                        command.Parameters.AddWithValue("@state", (int)user.State);
                        command.Parameters.AddWithValue("@messages", JsonConvert.SerializeObject(user.Messages));

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.UpdateUser: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }
    }
}
