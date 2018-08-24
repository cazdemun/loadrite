using System;
using System.Data;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;

namespace ConsoleApp2
{
    class Cleaner
    {
        enum Col { Fecha, Hora, Ubicacion, Operario, Cargadora, Producto, Funcion, Secuencia, Peso, Cliente, Ubicacion1, Camion, Direccion, Datos, Tara, Notas, Ciclo, Rendimiento, Mascara, Fecha_Mina, Turno, SecuenciaID, Index };

        //public static void Each<T>(this IEnumerable<T> ie, Action<T, int> action)
        //{
        //    var i = 0;
        //    foreach (var e in ie) action(e, i++);
        //}

        public static void PullData()
        {

        }

        public static DataTable cleanCSV(string path)
        {

            // Get Entity as List

            CopyDBEntities ctx = new CopyDBEntities();

            List<Prueba> PruebasList = ctx.Pruebas.ToList();

            // Create DataTable from new CSV

            DataTable Table = IOUtils.GetDataTabletFromCSVFile(path);

            // Starting cleaning Process - Delete Sustr Entries

            DataTable woSustrTable = Table.AsEnumerable().Where(x => x[(int)Col.Cargadora].ToString() != "Sustr").CopyToDataTable();

            // Continue cleaning Process - Add Columns

            addColumns(woSustrTable);

            // Copy Entity to DataTable

            DataTable PruebaDataTable = woSustrTable.Clone();

            foreach (Prueba p in PruebasList)
            {
                PruebaDataTable.Rows.Add(Utils.toDataRow(PruebaDataTable, p));
            }

            IOUtils.WriteToCsvFile(PruebaDataTable, @"C:\Users\lesch\Desktop\SQLtoDT.csv");

            // Delete last entry per Cargadora
            // CRITIC: Validate it belongs to a Imaginary Row i.e. Hour field is 7:00:00 or 19:00:00

            IEnumerable<Prueba> lastEntryPerCarg = PruebasList
                                                    .GroupBy(l => l.Cargadora)
                                                    .Select(g => g.OrderByDescending(c => Utils.toDateTime(c.Fecha.ToString()))
                                                                    .ThenByDescending(c => Utils.toHourTime(c.Hora.ToString()))
                                                                    .FirstOrDefault()
                                                    ).Select(r => r);

            foreach (Prueba row in lastEntryPerCarg)
            {
                Console.WriteLine("Deleting following imaginary rows...");
                Console.WriteLine(row.Fecha);
                Console.WriteLine(row.Hora);
                ctx.Pruebas.Remove(row);
                ctx.SaveChanges();
            }

            // Get info like last useful row per Carg or last sequence information per carg
            // CRITIC: This is going to be queried from PruebaDataTable

            Dictionary<string, Tuple<string, string>> usefulRows = PruebaDataTable
                                                                    .AsEnumerable()
                                                                    .GroupBy(l => l[(int)Col.Cargadora])
                                                                    .Select(g => g.OrderByDescending(c => Utils.toDateTime(c[(int)Col.Fecha].ToString()))
                                                                                    .ThenByDescending(c => Utils.toHourTime(c[(int)Col.Hora].ToString()))
                                                                                    .Skip(1).FirstOrDefault()
                                                                    ).ToDictionary(g => g[(int)Col.Cargadora].ToString(), g => Tuple.Create(g[(int)Col.Fecha].ToString().Trim(), g[(int)Col.Hora].ToString().Trim()));

            foreach (var row in usefulRows.Keys)
            {
                Console.WriteLine("Useful...");
                Console.WriteLine(row);
                Console.WriteLine(usefulRows[row]);
            }

            // Add Imaginary Rows
            // This process should consider the data useful row per Carg from the Previous Stage

            addImaginaryTurnTimeRows(ref woSustrTable, usefulRows);

            // Order Table so calculations such as cicle make sense

            DataTable augmentedTable = woSustrTable.AsEnumerable()
                                        .OrderBy(t => t[(int)Col.Cargadora])
                                        .ThenBy(t => DateTime.ParseExact(t[(int)Col.Fecha].ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture))
                                        .ThenBy(t => Utils.toHourTime(t[(int)Col.Hora].ToString()))
                                        .CopyToDataTable();


            Dictionary<Tuple<string, string>, Tuple<string, long>>
                secuenceLastValues = PruebaDataTable
                                        .AsEnumerable()
                                        .Where(l => Convert.ToInt64(l[(int)Col.SecuenciaID].ToString().Trim()) != 0)
                                        .GroupBy(l => l[(int)Col.Cargadora])
                                        .Select(g => g.OrderBy(c => c[(int)Col.SecuenciaID])
                                                        //.ThenBy(c => c[(int)Col.Producto])
                                                        .ThenByDescending(c => Utils.toHourTime(c[(int)Col.Hora].ToString()))
                                                        .FirstOrDefault()
                                        ).ToDictionary(g => Tuple.Create(g[(int)Col.Cargadora].ToString().Trim(), 
                                                                         g[(int)Col.Producto].ToString().Trim()),
                                                        g => Tuple.Create(g[(int)Col.Funcion].ToString().Trim(),
                                                                          Convert.ToInt64(g[(int)Col.SecuenciaID].ToString().Trim())));
            //PruebaDataTable
            //            .AsEnumerable()
            //            .Where(l => Convert.ToInt64(l[(int)Col.SecuenciaID].ToString().Trim()) != 0)
            //            .ToList()
            //            .ForEach(x => { Console.WriteLine(x); });



            foreach (var key in secuenceLastValues.Keys)
            {
                Console.WriteLine(key);
                Console.WriteLine(secuenceLastValues[key]);
            }

            long lastSequence = PruebaDataTable
                                        .AsEnumerable()
                                        .OrderByDescending(l => l[(int)Col.SecuenciaID].ToString().Trim())
                                        .Select(l => Convert.ToInt64(l[(int)Col.SecuenciaID].ToString()))
                                        .FirstOrDefault();

            Console.WriteLine("Last Sequence");
            Console.WriteLine(lastSequence);

            // This can be optimized with for each and linq

            for (int i = 0; i < augmentedTable.Rows.Count; i++)
            {
                DataRow row = augmentedTable.Rows[i];

                if (i == 0 && usefulRows.ContainsKey(row[(int)Col.Cargadora].ToString()))
                {
                    DataRow useful = augmentedTable.NewRow();
                    useful[(int)Col.Fecha] = usefulRows[row[(int)Col.Cargadora].ToString()].Item1.ToString().Trim();
                    useful[(int)Col.Hora] = usefulRows[row[(int)Col.Cargadora].ToString()].Item2.ToString().Trim();
                    useful[(int)Col.Cargadora] = row[(int)Col.Cargadora];
                    row[(int)Col.Ciclo] = calculateCicleTime(useful, row);
                }

                if (i > 0)
                {
                    DataRow prevRow = augmentedTable.Rows[i - 1];
                    if (row[(int)Col.Cargadora].ToString() != prevRow[(int)Col.Cargadora].ToString() && usefulRows.ContainsKey(row[(int)Col.Cargadora].ToString()))
                    {
                        DataRow useful = augmentedTable.NewRow();
                        useful[(int)Col.Fecha] = usefulRows[row[(int)Col.Cargadora].ToString()].Item1.ToString().Trim();
                        useful[(int)Col.Hora] = usefulRows[row[(int)Col.Cargadora].ToString()].Item2.ToString().Trim();
                        useful[(int)Col.Cargadora] = row[(int)Col.Cargadora];
                        row[(int)Col.Ciclo] = calculateCicleTime(useful, row);
                    } else
                    {
                        row[(int)Col.Ciclo] = calculateCicleTime(prevRow, row);

                    }
                    // Get first per carg and update with corresponding useful row
                }

                row[(int)Col.Rendimiento] = calculatePerformance(row);

                row[(int)Col.Fecha_Mina] = shiftDates(row);

                row[(int)Col.Turno] = calculateShift(row);

                row[(int)Col.Mascara] = calculateMask(row);

                if (row[(int)Col.Funcion].ToString().Trim() == "Agregar" || row[(int)Col.Funcion].ToString().Trim() == "Borrar total")
                {
                    string cargadora = row[(int)Col.Cargadora].ToString().Trim();
                    string producto = row[(int)Col.Producto].ToString().Trim();
                    Tuple<string, string> rowKey = Tuple.Create(cargadora, producto);

                    if (secuenceLastValues.ContainsKey(rowKey))
                    {
                        if (secuenceLastValues[rowKey].Item1 == "Borrar total")
                        {
                            lastSequence++;
                            row[(int)Col.SecuenciaID] = lastSequence.ToString();
                            secuenceLastValues[rowKey] = Tuple.Create(secuenceLastValues[rowKey].Item1, lastSequence);
                        }
                        else
                        {
                            row[(int)Col.SecuenciaID] = secuenceLastValues[rowKey].Item2.ToString();
                        }
                    }
                    else
                    {
                        lastSequence++;
                        row[(int)Col.SecuenciaID] = lastSequence.ToString();
                        secuenceLastValues[rowKey] = Tuple.Create(row[(int)Col.Funcion].ToString().Trim(), lastSequence);
                    }
                }
            }

            //var cargsLookUp = augmentedTable.AsEnumerable().ToLookup(x => x[(int)Col.Cargadora]);
            //IEnumerable<string> cargs = cargsLookUp.Select(g => g.Key.ToString()).AsEnumerable();

            //foreach (string s in cargs)
            //{   
            //    cargsLookUp[s].ToList();
            //}

            //Dictionary<string, Dictionary<string, Prueba>> cargadoraProductoRow = new Dictionary<string, Dictionary<string, Prueba>>();


            //cargadoraProductoRow["5"].Add("Desmonte", new Prueba());

            // Little correction
            //DataColumn indexColumn = new DataColumn("Index");
            //indexColumn.AutoIncrement = true;
            //augmentedTable.Columns.Add(indexColumn);


            // Calculate Borrar Total Transformation - This should consider 

            for (int i = 0; i < augmentedTable.Rows.Count; i++)
            {
                DataRow row = augmentedTable.Rows[i];
                row[(int)Col.Index] = i.ToString(); // Index algorithm may be wrong

                if ((row[(int)Col.Hora].ToString() == "7:00:00" || row[(int)Col.Hora].ToString() == "19:00:00") && i < augmentedTable.Rows.Count - 1)
                {
                    DataRow nextRow = augmentedTable.Rows[i + 1];
                    nextRow[(int)Col.Mascara] = "Tiempo No Disponible";
                }
            }


            //Cargadora 
            // Dictionary<Tuple<string, string>, Tuple<string, string>> cardAndProdToFuncAndSec =
            // This also should return a sum of the last sequences, but I'm not there yet

            //cargadora + producto => funcion + secuencia

            

           //augmentedTable.AsEnumerable().ToList().Each((row, index) =>
           //{
           //    // Update Dictionary

           //    if (row[(int)Col.Funcion].ToString().Trim() != "Agregar" || row[(int)Col.Funcion].ToString().Trim()  != "Borrar total")
           //    {
                   
           //    }


           //});

             var queryJustValidColumns = augmentedTable.Select().Where(x => (x[(int)Col.Secuencia].ToString() == "1")
            || x[(int)Col.Funcion].ToString().Contains("Borrar"));

            if (queryJustValidColumns.Count() != 0)
            {

                DataTable justValidColumns = queryJustValidColumns.CopyToDataTable();

                List<Tuple<Int32, Int32>> indexCollection = new List<Tuple<Int32, Int32>>();

                for (int i = 0; i < justValidColumns.Rows.Count - 1; i++)
                {
                    DataRow row = justValidColumns.Rows[i];
                    DataRow nextRow = justValidColumns.Rows[i + 1];

                    if (row[(int)Col.Cargadora].ToString() != nextRow[(int)Col.Cargadora].ToString())
                        continue;

                    if (row[(int)Col.Funcion].ToString() == "Agregar" && nextRow[(int)Col.Funcion].ToString().Contains("Borrar"))
                    {
                        indexCollection.Add(new Tuple<Int32, Int32>(Convert.ToInt32(row[(int)Col.Index].ToString()), Convert.ToInt32(nextRow[(int)Col.Index].ToString())));
                    }
                }

                foreach (var t in indexCollection)
                {

                    double sumaRealPeso = 0;
                    double sumaRealCiclo = 0;

                    for (int i = t.Item1; i < t.Item2; i++)
                    {
                        DataRow row = augmentedTable.Rows[i];

                        if (row[(int)Col.Funcion].ToString() == "Agregar" && row[(int)Col.Mascara].ToString() == "Tiempo Efectivo")
                        {
                            sumaRealPeso += double.Parse(row[(int)Col.Peso].ToString(), CultureInfo.GetCultureInfo("es-MX"));
                        }
                        if (row[(int)Col.Secuencia].ToString() != "1")
                        {
                            sumaRealCiclo += double.Parse(row[(int)Col.Ciclo].ToString());
                        }
                    }
                    DataRow borrartotal = augmentedTable.Rows[t.Item2];
                    sumaRealCiclo += double.Parse(borrartotal[(int)Col.Ciclo].ToString());

                    borrartotal[(int)Col.Ciclo] = sumaRealCiclo.ToString();
                    borrartotal[(int)Col.Peso] = sumaRealPeso.ToString();
                }
            }

            //WriteToCsvFile(justValidColumns, "C:\\Users\\lesch\\Desktop\\test2.CSV");

            return augmentedTable;
        }

        public static string calculateCicleTime(DataRow now, DataRow next)
        {
            if (now[(int)Col.Cargadora].ToString() != next[(int)Col.Cargadora].ToString())
                return null;

            DateTime a = DateTime.ParseExact(now[(int)Col.Fecha].ToString() + ' ' + now[(int)Col.Hora].ToString(), "dd/MM/yyyy H:mm:ss", CultureInfo.InvariantCulture);
            DateTime b = DateTime.ParseExact(next[(int)Col.Fecha].ToString() + ' ' + next[(int)Col.Hora].ToString(), "dd/MM/yyyy H:mm:ss", CultureInfo.InvariantCulture);

            double diff = b.Subtract(a).TotalSeconds;

            if (diff > 43200)
                return null;

            return diff.ToString();
        }

        public static string calculatePerformance(DataRow row)
        {
            if (row[(int)Col.Funcion].ToString() != "Agregar" || row[(int)Col.Ciclo].ToString() == "") // CRITIC
                return null;

            double peso = double.Parse(row[(int)Col.Peso].ToString(), CultureInfo.GetCultureInfo("es-MX"));
            double ciclo = Convert.ToDouble(row[(int)Col.Ciclo].ToString());

            return (peso / (ciclo / 3600)).ToString();
        }

        public static string shiftDates(DataRow row)
        {
            DateTime today = DateTime.ParseExact(row[(int)Col.Fecha].ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture);

            DateTime flag = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
            DateTime now = DateTime.ParseExact(row[(int)Col.Hora].ToString(), "H:mm:ss", CultureInfo.InvariantCulture);


            if (now < flag)
                return today.AddDays(-1).ToString("dd/MM/yyyy");

            return row[(int)Col.Fecha].ToString();
        }

        public static string calculateShift(DataRow row)
        {
            DateTime morning = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
            DateTime evening = DateTime.ParseExact("19:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

            DateTime now = DateTime.ParseExact(row[(int)Col.Hora].ToString(), "H:mm:ss", CultureInfo.InvariantCulture);

            if (now <= morning || now > evening)
                return "Noche";

            return "Dia";
        }

        public static string calculateMask(DataRow row)
        {

            if (row[(int)Col.Hora].ToString() == "7:00:00" || row[(int)Col.Hora].ToString() == "19:00:00")
                return "Tiempo No Disponible";

            if (row[(int)Col.Funcion].ToString() != "Agregar" || row[(int)Col.Ciclo].ToString() == "") // CRITIC
                return "Demora/StandBy";

            double peso = double.Parse(row[(int)Col.Peso].ToString(), CultureInfo.GetCultureInfo("es-MX"));
            double ciclo = Convert.ToDouble(row[(int)Col.Ciclo].ToString());

            if (peso < 2 || peso > 14 || ciclo < 40 || ciclo > 600)
                return "Demora/StandBy";

            return "Tiempo Efectivo";
        }

        public static bool isSameTurn (DataRow row, Dictionary<string, Tuple<string, string>> usefulRows) {

            string cargadora = row[(int)Col.Cargadora].ToString();

            if (!(usefulRows.ContainsKey(cargadora)))
            {
                return false;
            }

            DateTime fecha = Utils.toDateTime(row[(int)Col.Fecha].ToString());
            DateTime hora = Utils.toHourTime(row[(int)Col.Hora].ToString());

            DateTime usefulFecha = Utils.toDateTime(usefulRows[cargadora].Item1);
            DateTime usefulHora = Utils.toHourTime(usefulRows[cargadora].Item2);

            DateTime morning = Utils.toHourTime("7:00:00");
            DateTime late = Utils.toHourTime("19:00:00");

            if ( fecha == usefulFecha && ( (hora < morning && usefulHora < morning) 
                                        || (hora > morning && usefulHora > morning && hora < late && usefulHora < late) 
                                        || (hora > late && usefulHora > late) ))
            {
                return true;
            }

            if (fecha < usefulFecha && hora < morning && usefulHora > late)
            {
                return true;
            }

            return true;
        }

        public static bool isSameTurn(Registry row, Dictionary<string, Tuple<string, string>> usefulRows)
        {

            if (!(usefulRows.ContainsKey(row.Cargadora)))
                return false;

            DateTime usefulFecha = Utils.toDateTime(usefulRows[row.Cargadora].Item1);
            DateTime usefulHora = Utils.toHourTime(usefulRows[row.Cargadora].Item2);

            DateTime morning = Utils.toHourTime("7:00:00");
            DateTime late = Utils.toHourTime("19:00:00");

            if (row.Fecha == usefulFecha && ((row.Hora < morning && usefulHora < morning)
                                        || (row.Hora > morning && usefulHora > morning && row.Hora < late && usefulHora < late)
                                        || (row.Hora > late && usefulHora > late)))
            {
                return true;
            }

            if (row.Fecha < usefulFecha && row.Hora < morning && usefulHora > late)
                return true;

            return true;
        }

        public static void addImaginaryRows(List<Registry> dt, Dictionary<string, Tuple<string, string>> usefulRows)
        {
            DateTime morningHour = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
            DateTime eveningHour = DateTime.ParseExact("19:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

            dt.ForEach(row =>
            {
                if (row.Hora < morningHour)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        Registry yesterdayEvening = new Registry
                        {
                            Fecha = row.Fecha.AddDays(-1),
                            Hora = eveningHour,
                            Cargadora = row.Cargadora
                        };

                        dt.Add(yesterdayEvening);
                    }

                    Registry morning = new Registry
                    {
                        Fecha = row.Fecha,
                        Hora = morningHour,
                        Cargadora = row.Cargadora
                    };

                    dt.Add(morning);
                }

                if (row.Hora > morningHour && row.Hora < eveningHour)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        Registry morning = new Registry
                        {
                            Fecha = row.Fecha,
                            Hora = morningHour,
                            Cargadora = row.Cargadora
                        };

                        dt.Add(morning);
                    }

                    Registry evening = new Registry
                    {
                        Fecha = row.Fecha,
                        Hora = eveningHour,
                        Cargadora = row.Cargadora
                    };

                    dt.Add(evening);
                }

                if (row.Hora > eveningHour)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        Registry evening = new Registry
                        {
                            Fecha = row.Fecha,
                            Hora = eveningHour,
                            Cargadora = row.Cargadora
                        };

                        dt.Add(evening);
                    }

                    Registry tomorrowMorning = new Registry
                    {
                        Fecha = row.Fecha.AddDays(1),
                        Hora = morningHour,
                        Cargadora = row.Cargadora
                    };

                    dt.Add(tomorrowMorning);
                }
            });
        }

           public static void addImaginaryTurnTimeRows(ref DataTable dt, Dictionary<string, Tuple<string, string>> usefulRows)
        {
            DataTable newTable = dt.Clone();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow row = dt.Rows[i];

                DateTime today = Utils.toDateTime(row[(int)Col.Fecha].ToString());
                DateTime now = Utils.toHourTime(row[(int)Col.Hora].ToString());

                DateTime morningNow = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
                DateTime lateNow = DateTime.ParseExact("19:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

                if (now < morningNow)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        DataRow yesterdayLate = newTable.NewRow();
                        yesterdayLate[(int)Col.Fecha] = today.AddDays(-1).ToString("dd/MM/yyyy");
                        yesterdayLate[(int)Col.Hora] = "19:00:00";
                        yesterdayLate[(int)Col.Cargadora] = row[(int)Col.Cargadora];
                        newTable.Rows.Add(yesterdayLate);
                    }

                    DataRow morning = newTable.NewRow();
                    morning[(int)Col.Fecha] = row[(int)Col.Fecha];
                    morning[(int)Col.Hora] = "7:00:00";
                    morning[(int)Col.Cargadora] = row[(int)Col.Cargadora]; ;
                    newTable.Rows.Add(morning);
                }

                if (now > morningNow && now < lateNow)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        DataRow morning = newTable.NewRow();
                        morning[(int)Col.Fecha] = row[(int)Col.Fecha];
                        morning[(int)Col.Hora] = "7:00:00";
                        morning[(int)Col.Cargadora] = row[(int)Col.Cargadora]; ;
                        newTable.Rows.Add(morning);
                    }

                    DataRow late = newTable.NewRow();
                    late[(int)Col.Fecha] = row[(int)Col.Fecha];
                    late[(int)Col.Hora] = "19:00:00";
                    late[(int)Col.Cargadora] = row[(int)Col.Cargadora]; ;
                    newTable.Rows.Add(late);
                }

                if (now > lateNow)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        DataRow late = newTable.NewRow();
                        late[(int)Col.Fecha] = row[(int)Col.Fecha];
                        late[(int)Col.Hora] = "19:00:00";
                        late[(int)Col.Cargadora] = row[(int)Col.Cargadora]; ;
                        newTable.Rows.Add(late);
                    }

                    DataRow tomorrowMorning = newTable.NewRow();
                    tomorrowMorning[(int)Col.Fecha] = today.AddDays(1).ToString("dd/MM/yyyy");
                    tomorrowMorning[(int)Col.Hora] = "7:00:00";
                    tomorrowMorning[(int)Col.Cargadora] = row[(int)Col.Cargadora]; ;
                    newTable.Rows.Add(tomorrowMorning);

                }
            }

            //foreach (DataRow dataRow in newTable.Rows)
            //{
            //    foreach (var item in dataRow.ItemArray)
            //    {
            //        Console.Write(item.ToString().Trim());
            //        Console.Write(" // ");
            //    }
            //    Console.WriteLine();
            //}
            Console.WriteLine("FILTER");

            DataTable fTable = newTable.AsEnumerable().OrderBy(t => t[(int)Col.Cargadora])
                                            .ThenBy(t => Utils.toDateTime(t[(int)Col.Fecha].ToString()))
                                            .ThenBy(t => Utils.toHourTime(t[(int)Col.Hora].ToString()))
                                            .Distinct().CopyToDataTable()
                                            .DefaultView.ToTable(true);

            //foreach (DataRow dataRow in fTable.Rows)
            //{
            //    foreach (var item in dataRow.ItemArray)
            //    {
            //        Console.Write(item.ToString().Trim());
            //        Console.Write(" // ");
            //    }
            //    Console.WriteLine();
            //}

            foreach (DataRow drtableOld in fTable.Rows)
            {
                dt.ImportRow(drtableOld);
            }
        }

        public static void addColumns(DataTable dt)
        {
            DataColumn diffColumn = new DataColumn("Ciclo");
            diffColumn.AllowDBNull = true;
            dt.Columns.Add(diffColumn);

            DataColumn rendColumn = new DataColumn("Rendimiento");
            rendColumn.AllowDBNull = true;
            dt.Columns.Add(rendColumn);

            DataColumn mascColumn = new DataColumn("Mascara");
            mascColumn.AllowDBNull = true;
            dt.Columns.Add(mascColumn);

            DataColumn fmColumn = new DataColumn("Fecha Mina");
            fmColumn.AllowDBNull = true;
            dt.Columns.Add(fmColumn);

            DataColumn turnColumn = new DataColumn("Turno");
            turnColumn.AllowDBNull = true;
            dt.Columns.Add(turnColumn);

            DataColumn secColumn = new DataColumn("SecuenciaID");
            secColumn.AllowDBNull = true;
            dt.Columns.Add(secColumn);

            DataColumn indexColumn = new DataColumn("Index");
            //indexColumn.AutoIncrement = true;
            turnColumn.AllowDBNull = true;
            dt.Columns.Add(indexColumn);
        }

        public static bool testDTCicleTime(DataTable dt)
        {
            var datesByCarg = dt.Select().ToLookup(x => x[(int)Col.Cargadora], x => Tuple.Create(x[(int)Col.Hora], x[(int)Col.Ciclo]));

            var keys = datesByCarg.Select(g => g.Key).ToList();

            foreach (object key in keys)
            {
                Console.Write(key);
                Console.WriteLine();


                System.Collections.Generic.List<double> totalSums = new System.Collections.Generic.List<double>();
                totalSums.Add(0);
                int cont = 0;
                foreach (object date in datesByCarg[key])
                {
                    Tuple<object, object> selectedTuple = (Tuple<object, object>)date;
                    totalSums[cont] += selectedTuple.Item2.ToString() == "" ? 0 : Convert.ToDouble(selectedTuple.Item2.ToString());
                    if (selectedTuple.Item1.ToString() == "7:00:00" || selectedTuple.Item1.ToString() == "19:00:00")
                    {
                        cont++;
                        totalSums.Add(0);
                    }
                }

                foreach (double n in totalSums)
                {
                    Console.Write(n);
                    Console.WriteLine();
                }
            }
            return true;
        }


    }
}
