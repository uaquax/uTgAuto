using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS User (
                            ID INTEGER PRIMARY KEY AUTOINCREMENT,
                            ChatID INTEGER UNIQUE,
                            ApiID TEXT,
                            ApiHash TEXT,
                            Phone TEXT,
                            Password TEXT,
                            Messages JSON,
                            ParallelMessages JSON,
                            Code INTEGER,
                            Coins REAL,
                            State INTEGER,
                            IsSignedUp INTEGER DEFAULT 1
                        );";

                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                ResetState();
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.Initialize: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        public void ResetState()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string updateQuery = @"
                        UPDATE User
                        SET State = @state
                        WHERE State >= 17;";

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
                LoggerService.Error($"DatabaseService.ResetState: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
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
                                    Coins = Convert.ToSingle(reader["Coins"]),
                                    State = (UserState)Convert.ToInt32(reader["State"]),
                                    Messages = JsonConvert.DeserializeObject<List<Message>>((string)reader["Messages"])!,
                                    ParallelMessages = JsonConvert.DeserializeObject<List<ParallelMessage>>((string)reader["ParallelMessages"])!,
                                    IsSignedUp = Convert.ToInt32(reader["IsSignedUp"]) == 1
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
                                    Coins = Convert.ToSingle(reader["Coins"]),
                                    State = (UserState)Convert.ToInt32(reader["State"]),
                                    Messages = JsonConvert.DeserializeObject<List<Message>>((string)reader["Messages"])!,
                                    ParallelMessages = JsonConvert.DeserializeObject<List<ParallelMessage>>((string)reader["ParallelMessages"])!,
                                    IsSignedUp = Convert.ToInt32(reader["IsSignedUp"]) == 1
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
                        INSERT INTO User (ChatID, ApiID, ApiHash, Phone, Password, Code, Coins, State, Messages, ParallelMessages, IsSignedUp)
                        VALUES (@ChatID, @ApiID, @ApiHash, @Phone, @Password, @Code, @Coins, @State, @Messages, @ParallelMessages, @IsSignedUp);";

                    using (var command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ChatID", user.ChatID);
                        command.Parameters.AddWithValue("@ApiID", user.ApiID);
                        command.Parameters.AddWithValue("@ApiHash", user.ApiHash);
                        command.Parameters.AddWithValue("@Phone", user.Phone);
                        command.Parameters.AddWithValue("@Password", user.Password);
                        command.Parameters.AddWithValue("@Code", user.Code);
                        command.Parameters.AddWithValue("@Coins", user.Coins);
                        command.Parameters.AddWithValue("@State", (int)user.State);
                        command.Parameters.AddWithValue("@Messages", JsonConvert.SerializeObject(user.Messages));
                        command.Parameters.AddWithValue("@ParallelMessages", JsonConvert.SerializeObject(user.ParallelMessages));
                        command.Parameters.AddWithValue("@IsSignedUp", user.IsSignedUp ? 1 : 0);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().ToLower().Contains("unique")) return;
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

                    string updateQuery = @"
                        UPDATE User
                        SET ApiID = 0, ApiHash = '', Phone = '', Password = '', Code = 0, State = 0, Messages = '[]', ParallelMessages = '[]', IsSignedUp = 1
                        WHERE ChatID = @chatid;";

                    using (var command = new SQLiteCommand(updateQuery, connection))
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

        public void AddCoins(long chatID, float amount)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string updateQuery = @"
                        UPDATE User
                        SET Coins = Coins + @amount
                        WHERE ChatID = @chatID;";

                    using (var command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@chatID", chatID);
                        command.Parameters.AddWithValue("@amount", amount);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.AddCoins: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        public void RemoveCoins(long chatID, float amount)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string updateQuery = @"
                        UPDATE User
                        SET Coins = Coins - @amount
                        WHERE ChatID = @chatID
                        AND Coins >= @amount;";

                    using (var command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@chatID", chatID);
                        command.Parameters.AddWithValue("@amount", amount);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"DatabaseService.RemoveCoins: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
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
                            Coins = @coins,
                            State = @state,
                            Messages = @messages,
                            ParallelMessages = @parallelMessages,
                            IsSignedUp = @isSignedUp
                        WHERE ChatID = @chatID;";

                    using (var command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@chatID", user.ChatID);
                        command.Parameters.AddWithValue("@apiID", user.ApiID);
                        command.Parameters.AddWithValue("@apiHash", user.ApiHash);
                        command.Parameters.AddWithValue("@phone", user.Phone);
                        command.Parameters.AddWithValue("@password", user.Password);
                        command.Parameters.AddWithValue("@coins", user.Coins);
                        command.Parameters.AddWithValue("@state", (int)user.State);
                        command.Parameters.AddWithValue("@messages", JsonConvert.SerializeObject(user.Messages));
                        command.Parameters.AddWithValue("@parallelMessages", JsonConvert.SerializeObject(user.ParallelMessages));
                        command.Parameters.AddWithValue("@isSignedUp", user.IsSignedUp ? 1 : 0);

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
