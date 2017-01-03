using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using QC = System.Data.SqlClient;  // System.Data.dll  
using DT = System.Data;
using System.Text;
using System.Configuration;

namespace ExploreWiki.Controllers
{
    public class HomeController : Controller
    {
        public class InputPersonName
        {
            public string InputName { get; set; }
        }

        /// <summary>
        /// Class representing a node in graph.
        /// </summary>
        public class Person
        {
            /// <summary>
            /// Name of the node.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Unique id of the node.
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// Distance from the starting point.
            /// </summary>
            public int DistanceFromCenter { get; set; }

            /// <summary>
            /// Normalized name string.
            /// </summary>
            public string NormalizedName { get { return NormalizeString(Name); } }

            /// <summary>
            /// Color of the node.
            /// As we go away from the center color changes.
            /// </summary>
            public string GetRGBColor
            {
                get
                {
                    if (DistanceFromCenter == 0)
                    {
                        // Red.
                        return "rgb(255, 0, 0)";
                    }
                    else
                    {
                        // Gradually fade as you move from the center.
                        return string.Format("rgb(155, 155, {0}", Math.Max(200 - 10 * DistanceFromCenter, 70));
                    }
                }
            }

            /// <summary>
            /// Unique id generation.
            /// </summary>
            private static int lastUsedId = 0;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="distanceFromCeneter"></param>
            public Person(string name, int distanceFromCeneter)
            {
                Name = name;
                Id = lastUsedId++;
                DistanceFromCenter = distanceFromCeneter;
            }
        }

        /// <summary>
        /// Max number of nodes in graph.
        /// </summary>
        private const int MaxNumberOfPersonsPerGraph = 100;

        private const string DefaultProviderName = "System.Data.ProdDb";

        /// <summary>
        /// Class representing branch in the graph.
        /// </summary>
        public class Relation
        {
            public Person PersonFrom { get; set; }

            public Person PersonTo { get; set; }

            public string RelationName { get; set; }


            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="personFrom"></param>
            /// <param name="personTo"></param>
            /// <param name="relationName"></param>
            public Relation(Person personFrom, Person personTo, string relationName)
            {
                PersonFrom = personFrom;
                PersonTo = personTo;
                RelationName = relationName;
            }
        }

        /// <summary>
        /// List of all connections in graph.
        /// </summary>
        List<Relation> PersonConnections { get; set; }

        /// <summary>
        /// List of all nodes in graph.
        /// </summary>
        Dictionary<string, Person> Persons { get; set; }

        Queue<Person> processingQueue { get; set; }

        /// <summary>
        /// Generating helper message from the time needed to generate the graph.
        /// </summary>
        TimeSpan GenerationTook { get; set; }

        /// <summary>
        /// String normalization state machine.
        /// </summary>
        enum StringNormalizerState
        {
            NormalChar,
            FirstPercentage,
            FirstSpecialChar,
            SecondPercentage,
            SecondSpecialChar,
        }

        /// <summary>
        /// Structure representing a node directly read from database.
        /// </summary>
        class GraphRelationRaw
        {
            public string Name { get; set; }
            public string From { get; set; }
            public string To { get; set; }
        }

        /// <summary>
        /// Normalize the input string.
        /// In wiki dump we store info in "url" like format with %20 as space, '%' escaping for unicode etc.
        /// For now normalization is done here.
        /// TODO: This should be really normalized in database itself.
        /// </summary>
        /// <param name="originalString">String to normalize.</param>
        /// <returns>Normalized string.</returns>
        private static string NormalizeString(string originalString)
        {
            List<char> normalizedChars = new List<char>();
            StringNormalizerState state = StringNormalizerState.NormalChar;

            List<char> specialBytes = new List<char>();
            int specialCharsCounter = 0;

            foreach (char currentChar in originalString)
            {
                switch(state)
                {
                    case StringNormalizerState.NormalChar:
                        if (currentChar == '%')
                        {
                            state = StringNormalizerState.FirstPercentage;
                        }
                        else
                        {
                            normalizedChars.Add(currentChar);
                        }
                        break;
                    case StringNormalizerState.FirstPercentage:
                        specialBytes.Add(currentChar);
                        specialCharsCounter++;

                        if (specialCharsCounter == 2)
                        {
                            state = StringNormalizerState.SecondPercentage;
                            specialCharsCounter = 0;
                        }

                        break;
                    case StringNormalizerState.SecondPercentage:
                        if (currentChar == '%')
                        {
                            state = StringNormalizerState.SecondSpecialChar;
                        }
                        else
                        {
                            // Something is strange, just return the original.
                            return originalString;
                        }
                        break;
                    case StringNormalizerState.SecondSpecialChar:
                        specialBytes.Add(currentChar);
                        specialCharsCounter++;

                        if (specialCharsCounter == 2)
                        {
                            state = StringNormalizerState.NormalChar;

                            // TODO: This is really not performant but will do for now.
                            byte byte1 = Convert.ToByte(new string(specialBytes.Take(2).ToArray()), 16);
                            byte byte2 = Convert.ToByte(new string(specialBytes.Skip(2).ToArray()), 16);

                            normalizedChars.AddRange(Encoding.UTF8.GetChars(new byte[] { byte1, byte2 }));

                            specialBytes.Clear();
                            specialCharsCounter = 0;
                        }

                        break;
                }
            }

            return (new string(normalizedChars.ToArray())).Replace("_", " ");
        }

        /// <summary>
        /// Put string into 'database' format.
        /// </summary>
        /// <param name="originalString"></param>
        /// <returns>Denormalized version of input string.</returns>
        private static string Denormalize(string originalString)
        {
            // TODO: when we do normalization in db this shouldn't be here.
            List<char> denormalizedChars = new List<char>();
            foreach (char currentChar in originalString)
            {
                if ((int)currentChar < 256)
                {
                    denormalizedChars.Add(currentChar);
                }
                else
                {
                    // break it.
                    denormalizedChars.Add('%');
                    denormalizedChars.AddRange(
                        BitConverter.ToString(Encoding.UTF8.GetBytes(new char[] { currentChar })).Replace('-', '%').ToArray()
                    );
                }
            }

            return (new string(denormalizedChars.ToArray())).Replace(" ", "_");
        }

        /// <summary>
        /// Action which builds the home page.
        /// </summary>
        /// <param name="inputNameModel">Input name from the main form.</param>
        /// <returns></returns>
        public ActionResult Index(InputPersonName inputNameModel)
        {
            Persons = new Dictionary<string, Person>();
            PersonConnections = new List<Relation>();
            processingQueue = new Queue<Person>();
            ViewBag.PersonNames = Persons;
            ViewBag.PersonConnections = PersonConnections;
            ViewBag.Message = "Enter person name to start graph traversal.";

            if (String.IsNullOrEmpty(inputNameModel.InputName))
            {
                // Nothing to do here. Waiting for user input.
                return View();
            }

            ViewBag.InputName = inputNameModel.InputName;

            Person startingPerson = new Person(Denormalize(inputNameModel.InputName), 0);
            Persons.Add(startingPerson.Name, startingPerson);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            BuildGraph(startingPerson, true, false);

            if (Persons.Count < 60)
            {
                // Restart search in aggressive mode.
                Persons.Clear();
                PersonConnections.Clear();
                BuildGraph(startingPerson, true, true, true);
            }

            GenerationTook = sw.Elapsed;

            ViewBag.PersonNames = Persons;
            ViewBag.PersonConnections = PersonConnections;
            ViewBag.GenerationTook = string.Format("{0}s for generating graph of {1} nodes.", GenerationTook.TotalSeconds, Persons.Count);
            return View();
        }

        private string GetConnectionString(string providerName)
        {
            ConnectionStringSettingsCollection settings = ConfigurationManager.ConnectionStrings;
            string returnValue = null;

            if (settings != null)
            {
                foreach (ConnectionStringSettings cs in settings)
                {
                    if (cs.ProviderName == providerName)
                        returnValue = cs.ConnectionString;
                    break;
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Main graph build method.
        /// Recursively builds the graph either until the size suffices or we are out of the nodes.
        /// </summary>
        /// <param name="startingPerson"></param>
        /// <param name="depth"></param>
        /// <param name="includePointingTo"></param>
        /// <param name="includeNonPersons"></param>
        /// <param name="aggressive"></param>
        private void BuildGraph(Person startingPerson, bool includePointingTo, bool includeNonPersons, bool aggressive = false)
        {
            List<GraphRelationRaw> branchList = new List<GraphRelationRaw>();

            // TODO: Don't open new connection every time.
            // Use connection pool.
            using (var connection = new QC.SqlConnection( GetConnectionString(HomeController.DefaultProviderName)))
            {
                connection.Open();
                using (var command = new QC.SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = DT.CommandType.Text;

                    // Queries here are not very optimized. Do better.
                    if (includePointingTo && !includeNonPersons)
                    {
                        command.CommandText = @"  
select entity_name as node_from, property_name as branch_name, link2 as node_to from properties1 
where link2 = @name and  exists (select name from persons where name = entity_name)
select entity_name as node_from, property_name as branch_name, link2 as node_to from properties1 
where entity_name = @name and link2 is not null and isPerson = 1
";
                    }
                    else if (!includePointingTo && includeNonPersons)
                    {

                        command.CommandText = @"  
select entity_name as node_from, property_name as branch_name, link2 as node_to from properties1 
where entity_name = @name and link2 is not null
";
                    }
                    else if (includePointingTo && includeNonPersons)
                    {
                        command.CommandText = @"
select entity_name as node_from, property_name as branch_name, link2 as node_to from properties1 
where (link2 = @name) or (entity_name = @name and link2 is not null)
";
                    }
                    else
                    {

                        command.CommandText = @"  
select entity_name as node_from, property_name as branch_name, link2 as node_to from properties1 
where entity_name = @name and link2 is not null and isPerson = 1
";
                    }

                    command.Parameters.AddWithValue("@name", startingPerson.Name);

                    QC.SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        branchList.Add(
                            new GraphRelationRaw {
                                From = reader.GetString(0),
                                Name = reader.GetString(1),
                                To = reader.GetString(2) });
                    }
                }
            }

            // Premature return if this is a dominant node
            // later in search.
            // This is an ugly edge case and should be removed at some point.
            if (branchList.Count > 20 && startingPerson.DistanceFromCenter > 1)
            {
                return;
            }

            foreach (var branch in branchList)
            {
                Person personFrom;
                Person personTo;

                bool isPersonFromNew = false;
                bool isPersonToNew = false;

                if (!Persons.TryGetValue(branch.From, out personFrom))
                {
                    personFrom = new Person(branch.From, startingPerson.DistanceFromCenter + 1);
                    Persons.Add(personFrom.Name, personFrom);
                    isPersonFromNew = true;
                    processingQueue.Enqueue(personFrom);
                }

                if (!Persons.TryGetValue(branch.To, out personTo))
                {
                    personTo = new Person(branch.To, startingPerson.DistanceFromCenter + 1);
                    Persons.Add(personTo.Name, personTo);
                    isPersonToNew = true;
                    processingQueue.Enqueue(personTo);
                }

                if (isPersonFromNew)
                {
                    personFrom.DistanceFromCenter = personTo.DistanceFromCenter + 1;
                }

                if (isPersonToNew)
                {
                    personTo.DistanceFromCenter = personFrom.DistanceFromCenter + 1;
                }

                PersonConnections.Add(new Relation(personFrom, personTo, branch.Name));
            }

            // One more check, if we are early in the graph and we still don't have many results
            // do more aggressive search.
            if (startingPerson.DistanceFromCenter == 0 && Persons.Count < 5 && (!includePointingTo || !includeNonPersons))
            {
                if (!includePointingTo)
                {
                    BuildGraph(startingPerson, true, false);
                }
                else
                {
                    // Do full search.
                    BuildGraph(startingPerson, true, true);
                }
            }

            while (processingQueue.Count != 0 && Persons.Count < MaxNumberOfPersonsPerGraph)
            {
                if (aggressive && startingPerson.DistanceFromCenter < 3)
                {
                    BuildGraph(processingQueue.Dequeue(), true, true, aggressive);
                }
                else if (aggressive && startingPerson.DistanceFromCenter < 4)
                {
                    BuildGraph(processingQueue.Dequeue(), true, false, aggressive);
                }
                if (startingPerson.DistanceFromCenter < 2 && Persons.Count < 5 && processingQueue.Count < 5)
                {
                    BuildGraph(processingQueue.Dequeue(), true, false);
                }
                else
                {
                    BuildGraph(processingQueue.Dequeue(), false, false);
                }
            }
        }

        /// <summary>
        /// Empty for now.
        /// </summary>
        /// <param name="inputNameModel"></param>
        /// <returns></returns>
        public ActionResult About(InputPersonName inputNameModel)
        {
            return View();
        }

        [HttpPost]
        public ActionResult UserInput(InputPersonName inputNameModel)
        {
            return View();
        }

        /// <summary>
        /// Empty for now.
        /// </summary>
        /// <returns></returns>
        public ActionResult Contact()
        {
            ViewBag.Title = "I should write something here but I am too lazy.";

            return View();
        }

        /// <summary>
        /// Auto complete for user inputs.
        /// </summary>
        /// <param name="term">Search pattern we are using for.</param>
        /// <returns></returns>
        public ActionResult Autocomplete(string term)
        {
            // Pulling this from database is a bit too slow.
            List<string> autocompleteItems = new List<string>();
            using (var connection = new QC.SqlConnection(GetConnectionString(HomeController.DefaultProviderName)))
            {
                connection.Open();
                using (var command = new QC.SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = DT.CommandType.Text;
                    command.CommandText = @"  
select top 10 name from persons1 
where name like @name
";

                    command.Parameters.AddWithValue("@name", Denormalize(term) + "%");

                    QC.SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        autocompleteItems.Add(NormalizeString(reader.GetString(0)));
                    }
                }
            }

            return Json(autocompleteItems.ToArray(), JsonRequestBehavior.AllowGet);
        }
    }
}