﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

//ADDED TO SAMPLE TO DEMONSTRATE Portable.Data.Sqlite
using Portable.Data.Sqlite;
using SampleApp.Shared.SqliteSampleCode;

namespace SampleApp.Wpf {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {

            #region ADDED TO SAMPLE TO DEMONSTRATE Portable.Data.Sqlite
            try {
                string myTableName = "TestTable1";
                string sql;

                //Get a handle to App and instantiate my "crypt engine"
                var myApp = (App)Application.Current;
                myApp.AppCryptEngine = myApp.AppCryptEngine ?? new FakeCryptEngine(myApp.AppPassword);

                #region Part 1 - ADO - Create a table, add a record, add a column, add encrypted data, read back data

                using (var dbConn = new SqliteAdoConnection(myApp.SqliteConnection, myApp.AppCryptEngine)) {

                    Console.WriteLine("PART 1 - Doing ADO stuff");

                    //Create the table if it doesn't exist
                    sql = "CREATE TABLE IF NOT EXISTS " + myTableName + " (IdColumn INTEGER PRIMARY KEY AUTOINCREMENT, DateTimeColumn DATETIME, TextColumn TEXT);";
                    using (var cmd = new SqliteCommand(sql, dbConn)) {
                        dbConn.SafeOpen();
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("Table [" + myTableName + "] created (if it didn't exist).");
                    }

                    //Add a record
                    sql = "INSERT INTO " + myTableName + " (DateTimeColumn, TextColumn) VALUES (@date, @text);";
                    int newRowId;
                    using (var cmd = new SqliteCommand(sql, dbConn)) {
                        cmd.Parameters.Add(new SqliteParameter("@date", DateTime.Now));
                        cmd.Parameters.Add(new SqliteParameter("@text", "Hello SQLite."));
                        dbConn.SafeOpen();
                        newRowId = Convert.ToInt32(cmd.ExecuteReturnRowId());  //Note: INTEGER columns in SQLite are always long/Int64 - including ID columns, so converting to int
                        Console.WriteLine("A record with ID " + newRowId.ToString() + " was created in table [" + myTableName + "].");
                    }

                    //Read the datetime column on the oldest record
                    sql = "SELECT [DateTimeColumn] FROM " + myTableName + " ORDER BY [DateTimeColumn] LIMIT 1;";
                    using (var cmd = new SqliteCommand(sql, dbConn)) {
                        dbConn.SafeOpen();
                        DateTime oldest = Convert.ToDateTime(cmd.ExecuteScalar());
                        Console.WriteLine("The oldest record in table [" + myTableName + "] has timestamp: " + oldest.ToString());
                    }

                    //Add an encrypted column to the table
                    //NOTE: There is no benefit to creating the column as SQLite datatype ENCRYPTED vs. TEXT
                    //  It is actually a TEXT column - but I think it is nice to set it to type ENCRYPTED for future purposes.
                    //  Hopefully a future version of SQLitePCL will make it easy to figure out if a column is defined as ENCRYPTED or TEXT
                    //  (right now, it identifies both as TEXT)
                    sql = "ALTER TABLE " + myTableName + " ADD COLUMN EncryptedColumn ENCRYPTED;";
                    //Note: This column shouldn't exist until the above sql is run, since I just created the table above.  But if this application has been run multiple times, 
                    //  the column may already exist in the table - so I need to check for it.
                    bool columnAlreadyExists = false;
                    #region Check for column
                    using (var checkCmd = new SqliteCommand(dbConn)) {
                        checkCmd.CommandText = "PRAGMA table_info (" + myTableName + ");";
                        dbConn.SafeOpen();
                        using (var checkDr = new SqliteDataReader(checkCmd)) {
                            while (checkDr.Read()) {
                                if (checkDr.GetString("NAME") == "EncryptedColumn") {
                                    Console.WriteLine("The [EncryptedColumn] column already exists.");
                                    columnAlreadyExists = true;
                                    break;
                                }
                            }
                        }
                    }
                    #endregion
                    if (!columnAlreadyExists) {
                        using (var cmd = new SqliteCommand(sql, dbConn)) {
                            dbConn.SafeOpen();
                            cmd.ExecuteNonQuery();
                            Console.WriteLine("The [EncryptedColumn] column was created in table [" + myTableName + "].");
                        }
                    }

                    //Add a record with an encrypted column value
                    sql = "INSERT INTO " + myTableName + " (DateTimeColumn, TextColumn, EncryptedColumn) VALUES (@date, @text, @encrypted);";
                    using (var cmd = new SqliteCommand(sql, dbConn)) {
                        cmd.Parameters.Add(new SqliteParameter("@date", DateTime.Now));
                        cmd.Parameters.Add(new SqliteParameter("@text", "Hello data."));
                        cmd.AddEncryptedParameter(new SqliteParameter("@encrypted",
                            Tuple.Create<string, string, string>("Hello", "encrypted", "data")));
                        dbConn.SafeOpen();
                        newRowId = Convert.ToInt32(cmd.ExecuteReturnRowId());  //Note: INTEGER columns in SQLite are always long/Int64 - including ID columns, so converting to int
                        Console.WriteLine("A record featuring encrypted data with ID " + newRowId.ToString() + " was created in table [" + myTableName + "].");
                    }

                    //Get the value of the encrypted column
                    sql = "SELECT [EncryptedColumn] FROM " + myTableName + " WHERE [IdColumn] = @id;";
                    using (var cmd = new SqliteCommand(sql, dbConn)) {
                        cmd.Parameters.Add(new SqliteParameter("@id", newRowId));
                        dbConn.SafeOpen();
                        string encryptedColumnValue = cmd.ExecuteScalar().ToString();
                        var decryptedValue = myApp.AppCryptEngine.DecryptObject<Tuple<string, string, string>>(encryptedColumnValue);
                        Console.WriteLine("The actual (encrypted) value of the [EncryptedColumn] column of record ID " + newRowId.ToString() + " is: " + encryptedColumnValue);
                        Console.WriteLine("The decrypted value of the [EncryptedColumn] column of record ID " + newRowId.ToString() + " is: " +
                            decryptedValue.Item1 + " " + decryptedValue.Item2 + " " + decryptedValue.Item3);
                    }

                    //Using a SqliteDataReader and GetDecrypted<T> to get all of the encrypted values
                    sql = "SELECT [IdColumn], [DateTimeColumn], [EncryptedColumn] FROM " + myTableName + ";";
                    using (var cmd = new SqliteCommand(sql, dbConn)) {
                        dbConn.SafeOpen();
                        using (var dr = new SqliteDataReader(cmd)) {
                            while (dr.Read()) {
                                var sb = new StringBuilder();
                                sb.Append("ID: " + dr.GetInt32("IdColumn").ToString());
                                sb.Append(" - Timestamp: " + dr.GetDateTime("DateTimeColumn").ToString());
                                //IMPORTANT: By default, GetDecrypted<T> will throw an exception on a NULL column value.  You can specify DbNullHandling.ReturnTypeDefaultValue
                                // to return the default value of the specified type - as in default(T) - when a NULL column value is encountered, if you choose.
                                var decryptedValue = dr.GetDecrypted<Tuple<string, string, string>>("EncryptedColumn", DbNullHandling.ReturnTypeDefaultValue);
                                sb.Append(" - Value: " + ((decryptedValue == null) ? "NULL" :
                                    decryptedValue.Item1 + " " + decryptedValue.Item2 + " " + decryptedValue.Item3));
                                Console.WriteLine(sb.ToString());
                            }
                        }
                    }
                }

                #endregion

                #region Part 2 - EncryptedTable - Create an encrypted table to hold SampleDataItem objects, and read and write data

                long numRecords;

                using (var dbConn = new SqliteAdoConnection(myApp.SqliteConnection, myApp.AppCryptEngine)) {

                    Console.WriteLine(" ");
                    Console.WriteLine("PART 2 - Doing EncryptedTable stuff");

                    //Creating the encrypted table, adding some items/records
                    using (var encTable = new EncryptedTable<SampleDataItem>(myApp.AppCryptEngine, dbConn)) {

                        //Shouldn't need to call CheckDbTable manually, but I am going to check to see if there are
                        //  records in the table, so I need to make sure the table exists
                        //This will check the table and create the table and/or any missing columns if needed.
                        encTable.CheckDbTable();

                        //Check to see how many records are in the table now
                        numRecords = SampleDataItem.GetNumRecords(dbConn, encTable.TableName);
                        Console.WriteLine("(1) There are currently " + numRecords.ToString() + " records in the table: " + encTable.TableName);

                        foreach (var item in ExampleData.GetData().Where(i => i.LastName != "Johnson")) {
                            encTable.AddItem(item);
                        }

                        Console.WriteLine("(2) There are currently {0} items to be written to the encrypted table: {1}",
                            encTable.TempItems.Where(i => i.IsDirty).Count(), encTable.TableName);

                        //Note that at this point in the code, nothing new has been written to the table yet.  
                        //  The table will be updated on encTable.Dispose (in this case, that happens automatically at the end of this using() code 
                        //  block) or we could force it now with encTable.WriteItemChanges()

                        //encTable.WriteItemChanges();
                    }

                    //Adding a couple more records...
                    using (var encTable = new EncryptedTable<SampleDataItem>(myApp.AppCryptEngine, dbConn)) {

                        //Because encTable was disposed above, we should now see records in the table
                        numRecords = SampleDataItem.GetNumRecords(dbConn, encTable.TableName);
                        Console.WriteLine("(3) There are currently " + numRecords.ToString() + " records in the table: " + encTable.TableName);

                        //Here is one way to add an item to the table - immediately
                        //  (no need to type out 'immediateWriteToTable:' - but just wanted to show what the 'true' was for)
                        encTable.AddItem(ExampleData.GetData().Where(i => i.FirstName == "Bob").Single(), immediateWriteToTable: true);

                        //Another way to add items to the table - wait until WriteItemChanges() or WriteChangesAndFlush() or encTable.Dispose()
                        //  is called
                        encTable.AddItem(ExampleData.GetData().Where(i => i.FirstName == "Joan").Single(), immediateWriteToTable: false);
                        encTable.AddItem(ExampleData.GetData().Where(i => i.FirstName == "Ned").Single());

                        //Should only see one more record - Joan and Ned haven't been written yet
                        numRecords = SampleDataItem.GetNumRecords(dbConn, encTable.TableName);
                        Console.WriteLine("(4) There are currently " + numRecords.ToString() + " records in the table: " + encTable.TableName);

                        //Let's see which items we have in memory right now
                        foreach (var item in encTable.TempItems) {
                            Console.WriteLine("In memory: ID#{0} {1} {2} - Status: {3}", item.Id, item.FirstName, item.LastName, item.SyncStatus);
                        }

                        //We can use WriteItemChanges() - writes any in-memory item changes to the table
                        //encTable.WriteItemChanges();

                        //OR WriteChangesAndFlush() writes any in-memory items to the table, and then drops any in-memory items and/or in-memory index of the table
                        //Normally, only items that are out-of-sync with the table are written, forceWriteAll causes all items (whether they have changed or not)
                        //  to be written
                        encTable.WriteChangesAndFlush(forceWriteAll: true);

                        //How many items in memory now?
                        Console.WriteLine("After WriteChangesAndFlush() there are now {0} items in memory.", encTable.TempItems.Count());

                        //How many records in the table?
                        numRecords = SampleDataItem.GetNumRecords(dbConn, encTable.TableName);
                        Console.WriteLine("After WriteChangesAndFlush() there are now {0} records in the table.", numRecords.ToString());
                    }

                    //Reading and searching for items/records
                    using (var encTable = new EncryptedTable<SampleDataItem>(myApp.AppCryptEngine, dbConn)) {

                        //Doing a GetItems() with an empty TableSearch (like the line below) will get all items
                        List<SampleDataItem> allItems = encTable.GetItems(new TableSearch());

                        foreach (var item in allItems) {
                            Console.WriteLine("In table: ID#{0} {1} {2}", item.Id, item.FirstName, item.LastName);
                        }

                        //Let's just get one item - exceptionOnMissingItem: true will throw an exception if the item wasn't found
                        // in the table; with exceptionOnMissingItem: false, we will just get a null
                        SampleDataItem singleItem = encTable.GetItem(allItems.First().Id, exceptionOnMissingItem: true);
                        Console.WriteLine("Found via ID: ID#{0} {1} {2}", singleItem.Id, singleItem.FirstName, singleItem.LastName);

                        //Because we did a full table GetItems() above, we should have a nice, searchable index of all of the 
                        // items in the table.  But let's check it and re-build if necessary
                        encTable.CheckFullTableIndex(rebuildIfExpired: true);

                        //Otherwise, we could just force a rebuild of the searchable index
                        //  encTable.BuildFullTableIndex();

                        //So, the easy way to find matching items, based on the full table index is to pass in a TableSearch
                        List<SampleDataItem> matchingItems = encTable.GetItems(new TableSearch {
                            SearchType = TableSearchType.MatchAll,  //Items must match all search criteria
                            MatchItems = {
                            new TableSearchItem("LastName", "Johnson", SearchItemMatchType.IsEqualTo),
                            new TableSearchItem("FirstName", "Ned", SearchItemMatchType.DoesNotContain)
                        }
                        });
                        foreach (var item in matchingItems) {
                            Console.WriteLine("Found via search: ID#{0} {1} {2}", item.Id, item.FirstName, item.LastName);
                        }

                        //Let's see what is in this "full table index" anyway
                        foreach (var item in encTable.FullTableIndex.Index) {
                            Console.WriteLine("Indexed item ID: " + item.Key.ToString());
                            foreach (var value in item.Value) {
                                Console.WriteLine("  - Searchable value: {0} = {1}", value.Key ?? "", value.Value ?? "");
                            }
                        }

                        //Let's remove/delete a record from the table (with immediate removal)
                        Console.WriteLine("Deleting record: ID#{0} {1} {2}", singleItem.Id, singleItem.FirstName, singleItem.LastName);
                        encTable.RemoveItem(singleItem.Id, immediateWriteToTable: true);

                        //Let's see what is actually in the table
                        Console.WriteLine("Records in the table " + encTable.TableName + " - ");
                        sql = "select * from " + encTable.TableName;
                        using (var cmd = new SqliteCommand(sql, dbConn)) {
                            using (SqliteDataReader reader = cmd.ExecuteReader()) {
                                while (reader.Read()) {
                                    var record = new StringBuilder();
                                    foreach (Portable.Data.Sqlite.TableColumn column in encTable.TableColumns.Values.OrderBy(tc => tc.ColumnOrder)) {
                                        if (column.ColumnName == "Id") { record.Append(column.ColumnName + ": " + reader[column.ColumnName].ToString()); }
                                        else {
                                            record.Append(" - " + column.ColumnName + ": " + (reader[column.ColumnName].ToString() ?? ""));
                                        }
                                    }
                                    Console.WriteLine(record.ToString());
                                }
                            }
                        }
                    }

                }

                #endregion

                Console.WriteLine("Done.");

                //Pop up a message saying we are done
                MessageBox.Show("If you can see this message, then the sample Portable.Data.Sqlite code ran correctly. " +
                    "Take a look at the code in the 'MainWindow.xaml.cs' file, and compare it to what you are seeing in the IDE Output window. " +
                    "And have a nice day...",
                    "All Done! - Check the Output window", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message + "\n\nDetails:\n" + ex.ToString(), "ERROR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            #endregion

        }
    }
}
