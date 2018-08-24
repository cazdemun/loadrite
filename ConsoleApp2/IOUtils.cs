using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    class IOUtils
    {
        public static DataTable GetDataTabletFromCSVFile(string csv_file_path)
        {
            DataTable csvData = new DataTable();
            try
            {
                using (TextFieldParser csvReader = new TextFieldParser(csv_file_path, Encoding.GetEncoding("iso-8859-1")))
                {
                    csvReader.SetDelimiters(new string[] { ";" });
                    csvReader.HasFieldsEnclosedInQuotes = true;
                    string[] colFields = csvReader.ReadFields();
                    foreach (string column in colFields)
                    {
                        string name = column;
                        while (csvData.Columns.Contains(name))
                            name = name + "1";

                        DataColumn datecolumn = new DataColumn(name);
                        datecolumn.AllowDBNull = true;
                        csvData.Columns.Add(datecolumn);
                    }
                    while (!csvReader.EndOfData)
                    {
                        string[] fieldData = csvReader.ReadFields();
                        //Making empty value as null
                        for (int i = 0; i < fieldData.Length; i++)
                        {
                            if (fieldData[i] == "")
                            {
                                fieldData[i] = null;
                            }
                        }
                        csvData.Rows.Add(fieldData);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return null;
            }
            return csvData;
        }

        public static List<Registry> GetListFromCSVFile(string csv_file_path)
        {
            List<Registry> registryData =new List<Registry>();
            try
            {
                using (TextFieldParser csvReader = new TextFieldParser(csv_file_path, Encoding.GetEncoding("iso-8859-1")))
                {
                    csvReader.SetDelimiters(new string[] { ";" });
                    csvReader.HasFieldsEnclosedInQuotes = true;
                    string[] colFields = csvReader.ReadFields();
                    while (!csvReader.EndOfData)
                    {
                        string[] fieldData = csvReader.ReadFields();
                        fieldData = fieldData.Select(x => x.Trim()).ToArray();

                        Registry actualData = new Registry();
                        actualData.Fecha = Utils.toDateTime(fieldData[0]);
                        actualData.Hora = Utils.toHourTime(fieldData[1]);
                        actualData.Ubicación = fieldData[2];
                        actualData.Operario = fieldData[3];
                        actualData.Cargadora = fieldData[4];
                        actualData.Producto = fieldData[5];
                        actualData.Función = fieldData[6];
                        actualData.Secuencia = fieldData[7];
                        actualData.Peso = fieldData[8] == "" ? (Nullable<double>)null : double.Parse(fieldData[8], CultureInfo.GetCultureInfo("es-MX"));
                        actualData.Actividad = fieldData[9];
                        actualData.Origen = fieldData[10];
                        actualData.Camión = fieldData[11];
                        actualData.Dirección = fieldData[12];
                        actualData.Datos5 = fieldData[13];
                        actualData.Tara = fieldData[14];
                        actualData.Notas = fieldData[15];
                        //
                        actualData.Ciclo = (Nullable<double>)null;
                        actualData.Rendimiento = (Nullable<double>)null;
                        actualData.Mascara = "";
                        actualData.FechaMina = Utils.toDateTime("01/01/2001");
                        actualData.Turno = "";
                        actualData.SecuenciaID = 0;

                        registryData.Add(actualData);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return null;
            }
            return registryData;
        }

        public static void WriteToCsvFile(DataTable dataTable, string filePath)
        {
            StringBuilder fileContent = new StringBuilder();

            foreach (var col in dataTable.Columns)
            {
                fileContent.Append(col.ToString() + ",");
            }

            fileContent.Replace(",", System.Environment.NewLine, fileContent.Length - 1, 1);

            foreach (DataRow dr in dataTable.Rows)
            {
                foreach (var column in dr.ItemArray)
                {
                    fileContent.Append("\"" + column.ToString() + "\",");
                }

                fileContent.Replace(",", System.Environment.NewLine, fileContent.Length - 1, 1);
            }

            System.IO.File.WriteAllText(filePath, fileContent.ToString());
        }
    }
}
