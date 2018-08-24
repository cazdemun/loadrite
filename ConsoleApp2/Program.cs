using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Net.NetworkInformation;



namespace ConsoleApp2
{
    class Program
    {
        
        static void Main(string[] args)
        {
            FileSystemWatcher observador = new FileSystemWatcher(@"C:\Users\lesch\Desktop\TheVoid");

            observador.NotifyFilter = (NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName);
            observador.Created += AlCambiar;
            observador.EnableRaisingEvents=true;
            Console.WriteLine("<Enter> para salir");
            Console.ReadLine();
        }
        static CopyDBEntities databaseManager = new CopyDBEntities();
        private static void AlCambiar(object source,FileSystemEventArgs e)
        {
            
            WatcherChangeTypes TipoDeCambio = e.ChangeType;
            if (e.Name.Substring(0, 3) =="MMS")
            {
                Console.WriteLine("El archivo {0} tuvo un cambio de: {1}", e.FullPath, TipoDeCambio.ToString());
                try
                {
                    System.Threading.Thread.Sleep(1000);
                    List<Registry> newRegistries = NewCleaner.NewCleaner.cleanCSV(e.FullPath);
                    List<Prueba> newPruebas = newRegistries.Select(r => Utils.toPrueba(r)).ToList();
                    CopyDBEntities ctx = new CopyDBEntities();

                    Console.WriteLine("* Starting writing...");
                    Console.WriteLine(newPruebas.Count);

                    ctx.Pruebas.AddRange(newPruebas);
                    ctx.SaveChanges();
                    Console.WriteLine("* Ending writing...");
                    Console.WriteLine("* Writing ended...");
                    //DataTable Table = Cleaner.cleanCSV(e.FullPath);

                    //foreach (DataRow row in Table.Rows)
                    //{
                    //    try
                    //    {
                    //        var loginInfo = databaseManager.PruebaProcedure(row[0].ToString(), row[1].ToString(), row[2].ToString(),
                    //                                                        row[3].ToString(), row[4].ToString(), row[5].ToString(),
                    //                                                        row[6].ToString(), row[7].ToString(), row[8].ToString(),
                    //                                                        row[9].ToString(), row[10].ToString(), row[11].ToString(),
                    //                                                        row[12].ToString(), row[13].ToString(), row[14].ToString(),
                    //                                                        row[15].ToString(), row[16].ToString(), row[17].ToString(),
                    //                                                        row[18].ToString(), row[19].ToString(), row[20].ToString(),
                    //                                                        row[21].ToString() == "" ? 0 : Convert.ToInt64(row[21]));
                    //        //Console.WriteLine("Error de Conexión con la Base de Datos");
                    //    }
                    //    catch (System.IO.IOException x)
                    //    {
                    //        Console.WriteLine("Error de Conexión con la Base de Datos");
                    //    }
                    //}

                    //var csv = File.ReadAllText(e.FullPath);
                    //var csv2 = csv.Split('\n');
                    //int cont = csv2.Length;

                    //for (int i = 0; i < cont; i++)
                    //{
                    //    if (i != 0)
                    //    {
                    //        String[] temp = csv2[i].Split(';');
                    //        if (temp.Length == 16)
                    //        {
                    //            try
                    //            {
                    //                var loginInfo = databaseManager.PruebaProcedure(temp[0], temp[1], temp[2], temp[3], temp[4], temp[5], temp[6], temp[7], temp[8], temp[9], temp[10], temp[11], temp[12], temp[13], temp[14], temp[15]);
                    //                Console.WriteLine("Error de Conexión con la Base de Datos");
                    //            }
                    //            catch (System.IO.IOException x)
                    //            {
                    //                Console.WriteLine("Error de Conexión con la Base de Datos");
                    //            }


                    //        }

                    //    }
                    //}
                    Console.WriteLine("Guardado exitoso");
                    Cleaner.PullData();
                    if (File.Exists(e.FullPath))
                    {
                        File.Delete(e.FullPath);
                    }
                }
                catch (System.IO.IOException x)
                {
                    Console.WriteLine(x.Message);

                }
            }
            else
            {
                Console.WriteLine("El documento guardado es incorrecto, y se procedió a borrarlo");
                if (File.Exists(e.FullPath))
                {
                    File.Delete(e.FullPath);
                }
                

            }

            


        }
    }
}
