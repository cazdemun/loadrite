using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2.NewCleaner
{
    class NewCleaner
    {
        public static List<Registry> cleanCSV(string path)
        {
            //CREATE OBJECT WITH THE DATA NEEDED

            List<Registry> newRegistries = IOUtils.GetListFromCSVFile(path);

            newRegistries = newRegistries.Where(r => r.Función != "Sustr").ToList();

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            CopyDBEntities ctx = new CopyDBEntities();
            List<Prueba> PruebasList = ctx.Pruebas.ToList();
            List<Registry> oldRegistries = new List<Registry>();

            PruebasList.ForEach(p =>
            {
                oldRegistries.Add(Utils.toRegistry(p));
            });
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            IEnumerable<Prueba> lastImaginaryRow = PruebasList
                                                    .GroupBy(l => l.Cargadora)
                                                    .Select(g => g.OrderByDescending(c => Utils.toDateTime(c.Fecha.ToString()))
                                                                    .ThenByDescending(c => Utils.toHourTime(c.Hora.ToString()))
                                                                    .FirstOrDefault()
                                                    ).Select(r => r);

            Console.WriteLine("* Deleting following imaginary rows...");
            foreach (Prueba row in lastImaginaryRow)
            {
                Console.Write(row.Cargadora.Trim());
                Console.Write(" - ");
                Console.Write(row.Fecha.Trim());
                Console.Write(" - ");
                Console.WriteLine(row.Hora.Trim());
                ctx.Pruebas.Remove(row);
                ctx.SaveChanges();
            }
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Dictionary<string, Tuple<DateTime, DateTime>>
                lastRegistryPerCarg = oldRegistries
                                        .GroupBy(l => l.Cargadora)
                                        .Select(g => g.OrderByDescending(c => c.Fecha)
                                                        .ThenByDescending(c => c.Hora)
                                                        .Skip(1).FirstOrDefault()
                                        ).ToDictionary(g => g.Cargadora,
                                                       g => Tuple.Create(g.Fecha, g.Hora));
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Console.WriteLine("* Last days per carg from previous registries...");
            lastRegistryPerCarg.Keys.ToList().ForEach(k =>
            {
                Console.Write(k);
                Console.Write(" - ");
                Console.WriteLine(lastRegistryPerCarg[k]);
            });
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            addImaginaryRows(newRegistries, lastRegistryPerCarg);

            newRegistries = newRegistries.OrderBy(t => t.Cargadora)
                                            .ThenBy(t => t.Fecha)
                                            .ThenBy(t => t.Hora)
                                            .ToList();
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Dictionary<Tuple<string, string>, Tuple<string, long>>
                secuenceLastValuesDesmonte = oldRegistries
                                        .Where(l => l.SecuenciaID != 0 && l.Producto == "Desmonte")
                                        .GroupBy(l => l.Cargadora)
                                        .Select(g => g.OrderBy(c => c.SecuenciaID)
                                                        .ThenByDescending(c => c.Hora)
                                                        .FirstOrDefault()
                                        ).ToDictionary(g => Tuple.Create(g.Cargadora,
                                                                         g.Producto),
                                                        g => Tuple.Create(g.Función,
                                                                          g.SecuenciaID));

            Dictionary<Tuple<string, string>, Tuple<string, long>>
                secuenceLastValuesMineral = oldRegistries
                                        .Where(l => l.SecuenciaID != 0 && l.Producto == "Mineral")
                                        .GroupBy(l => l.Cargadora)
                                        .Select(g => g.OrderBy(c => c.SecuenciaID)
                                                        .ThenByDescending(c => c.Hora)
                                                        .FirstOrDefault()
                                        ).ToDictionary(g => Tuple.Create(g.Cargadora,
                                                                         g.Producto),
                                                        g => Tuple.Create(g.Función,
                                                                          g.SecuenciaID));

            Dictionary<Tuple<string, string>, Tuple<string, long>>[] dictionaries = { secuenceLastValuesDesmonte, secuenceLastValuesMineral };

            Dictionary<Tuple<string, string>, Tuple<string, long>>
                secuenceLastValues = dictionaries.SelectMany(dict => dict)
                         .ToDictionary(pair => pair.Key, pair => pair.Value);

            Console.WriteLine("* Last sequence per cargadoras...");
            secuenceLastValues.Keys.ToList().ForEach(k =>
             {
                 Console.Write(k);
                 Console.Write(" - ");
                 Console.WriteLine(secuenceLastValues[k]);
             });
            var temp = secuenceLastValues.Keys.ToList();
            long lastSequence = temp.Count == 0 ? 0 : temp.Select(k => secuenceLastValues[k].Item2).ToList().Max(); ;
            Console.WriteLine("* Last sequence is... " + lastSequence.ToString());
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // I do really think this is bad practice, I'm abusing the reference property
            Dictionary<string, List<Registry>> byCarg = newRegistries
                                                        .GroupBy(g => g.Cargadora)
                                                        .ToDictionary(g => g.Key, g => g.ToList());

            Console.WriteLine("* Calculating Cicle Time...");
            Console.WriteLine("* Calculating Mine Date...");
            Console.WriteLine("* Calculating Shift...");
            byCarg.Keys.ToList().ForEach(k =>
            {
                Registry refFirst = byCarg[k].Take(1).First();

                if (lastRegistryPerCarg.ContainsKey(k))
                    refFirst.Ciclo = calculateCicleTime(new Registry { Cargadora = k, Fecha = lastRegistryPerCarg[k].Item1, Hora = lastRegistryPerCarg[k].Item2 }, refFirst);
                else
                    refFirst.Ciclo = null;

                for (int i = 0; i < byCarg[k].Count; i++)
                {
                    Registry r = byCarg[k][i];

                    if (i != 0)
                        r.Ciclo = calculateCicleTime(byCarg[k][i - 1], r);

                    r.Rendimiento = calculatePerformance(r);
                    r.FechaMina = calculateMineDate(r);
                    r.Turno = calculateShift(r);
                    r.Mascara = calculateMask(r);



                    if (r.Función == "Agregar" || r.Función == "Borrar total")
                    {
                        Tuple<string, string> rowKey = Tuple.Create(r.Cargadora, r.Producto);

                        if (secuenceLastValues.ContainsKey(rowKey))
                        {
                            if (secuenceLastValues[rowKey].Item1 == "Borrar total" || r.Secuencia == "1")
                            {
                                lastSequence++;
                                r.SecuenciaID = lastSequence;
                                secuenceLastValues[rowKey] = Tuple.Create(secuenceLastValues[rowKey].Item1, lastSequence);
                            }
                            else
                            {
                                r.SecuenciaID = secuenceLastValues[rowKey].Item2;
                            }
                        }
                        else
                        {
                            lastSequence++;
                            r.SecuenciaID = lastSequence;
                            secuenceLastValues[rowKey] = Tuple.Create(r.Función, lastSequence);
                        }
                    }
                }
            });
            Console.WriteLine("* Ending...");
            //for (int i = 0; i < newRegistries.Count; i++)
            //{
            //    Registry r = newRegistries[i];

            //    //if ((row[(int)Col.Hora].ToString() == "7:00:00" || row[(int)Col.Hora].ToString() == "19:00:00") && i < augmentedTable.Rows.Count - 1)
            //    //{
            //    //    DataRow nextRow = augmentedTable.Rows[i + 1];
            //    //    nextRow[(int)Col.Mascara] = "Tiempo No Disponible";
            //    //}
            //}

            return newRegistries;
        }
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static double? calculatePerformance(Registry row)
        {
            if (row.Función != "Agregar" || row.Ciclo == null) 
                return null;

            return (row.Peso / (row.Ciclo / 3600));
        }

        public static double? calculateCicleTime(Registry prev, Registry now)
        {
            if (prev.Cargadora != now.Cargadora)
                return null;

            DateTime a = DateTime.ParseExact(prev.Fecha.ToString("dd/MM/yyyy") + ' ' + prev.Hora.ToString("H:mm:ss"), "dd/MM/yyyy H:mm:ss", CultureInfo.InvariantCulture);
            DateTime b = DateTime.ParseExact(now.Fecha.ToString("dd/MM/yyyy") + ' ' + now.Hora.ToString("H:mm:ss"), "dd/MM/yyyy H:mm:ss", CultureInfo.InvariantCulture);

            double diff = b.Subtract(a).TotalSeconds;

            if (diff > 43200)
                return null;

            return diff;
        }

        public static DateTime calculateMineDate(Registry row)
        {
            DateTime morning = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

            if (row.Hora <= morning)
                return row.Fecha.AddDays(-1);

            return row.Fecha;
        }

        public static string calculateShift(Registry row)
        {
            DateTime morning = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
            DateTime evening = DateTime.ParseExact("19:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

            if (row.Hora <= morning || row.Hora > evening)
                return "NOCHE";

            return "DIA";
        }

        public static string calculateMask(Registry row)
        {
            DateTime morning = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
            DateTime evening = DateTime.ParseExact("19:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

            if (row.Hora == morning || row.Hora == evening)
                return "Tiempo No Disponible";

            if (row.Función != "Agregar" || row.Ciclo == null) // CRITIC
                return "Demora/StandBy";

            if (row.Peso < 2 || row.Peso > 14 || row.Ciclo < 40 || row.Ciclo > 600)
                return "Demora/StandBy";

            return "Tiempo Efectivo";
        }
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static bool isSameTurn(Registry row, Dictionary<string, Tuple<DateTime, DateTime>> usefulRows)
        {

            if (!(usefulRows.ContainsKey(row.Cargadora)))
                return false;

            DateTime usefulFecha = usefulRows[row.Cargadora].Item1;
            DateTime usefulHora = usefulRows[row.Cargadora].Item2;

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

        public static void addImaginaryRows(List<Registry> dt, Dictionary<string, Tuple<DateTime, DateTime>> usefulRows)
        {
            DateTime morningHour = DateTime.ParseExact("7:00:00", "H:mm:ss", CultureInfo.InvariantCulture);
            DateTime eveningHour = DateTime.ParseExact("19:00:00", "H:mm:ss", CultureInfo.InvariantCulture);

            List<Registry> imaginaryRows = new List<Registry>();

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
                            Cargadora = row.Cargadora,
                            Turno = "DIA"
                        };

                        imaginaryRows.Add(yesterdayEvening);
                    }

                    Registry morning = new Registry
                    {
                        Fecha = row.Fecha,
                        Hora = morningHour,
                        Cargadora = row.Cargadora,
                        Turno = "NOCHE"
                    };

                    imaginaryRows.Add(morning);
                }

                if (row.Hora > morningHour && row.Hora < eveningHour)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        Registry morning = new Registry
                        {
                            Fecha = row.Fecha,
                            Hora = morningHour,
                            Cargadora = row.Cargadora,
                            Turno = "NOCHE"
                        };

                        imaginaryRows.Add(morning);
                    }

                    Registry evening = new Registry
                    {
                        Fecha = row.Fecha,
                        Hora = eveningHour,
                        Cargadora = row.Cargadora,
                        Turno = "DIA"
                    };

                    imaginaryRows.Add(evening);
                }

                if (row.Hora > eveningHour)
                {
                    if (!(isSameTurn(row, usefulRows)))
                    {
                        Registry evening = new Registry
                        {
                            Fecha = row.Fecha,
                            Hora = eveningHour,
                            Cargadora = row.Cargadora,
                            Turno = "DIA"
                        };

                        imaginaryRows.Add(evening);
                    }

                    Registry tomorrowMorning = new Registry
                    {
                        Fecha = row.Fecha.AddDays(1),
                        Hora = morningHour,
                        Cargadora = row.Cargadora,
                        Turno = "NOCHE"
                    };

                    imaginaryRows.Add(tomorrowMorning);
                }
            });

            imaginaryRows = imaginaryRows.Distinct(new Comparer()).ToList();
            imaginaryRows.ForEach(r => { dt.Add(r); });

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Console.WriteLine("* Adding new imaginary rows...");

            //imaginaryRows.ForEach(r => {
            //    Console.Write(r.Cargadora);
            //    Console.Write(" - ");
            //    Console.Write(r.Fecha.ToString("dd/MM/yyyy"));
            //    Console.Write(" - ");
            //    Console.WriteLine(r.Hora.ToString("H:mm:ss"));
            //});
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            
        }
    }

    public class Comparer : IEqualityComparer<Registry>
    {
        public bool Equals(Registry x, Registry y)
        {
            return (x.Cargadora == y.Cargadora && x.Fecha == y.Fecha && x.Hora == y.Hora);
        }

        public int GetHashCode(Registry obj)
        {
            return (int)Convert.ToInt32(obj.Cargadora);
        }
    }
}
