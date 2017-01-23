using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using QC = System.Data.SqlClient;  // System.Data.dll  
using DT = System.Data;
using System.Configuration;
using ExploreWiki.Helpers;
using ExploreWiki.Models;
using ExploreWiki.ViewModel;
using Newtonsoft.Json;

namespace ExploreWiki.Controllers
{
    public class HomeController : Controller
    {
        /// <summary>
        /// Max number of nodes in graph.
        /// </summary>
        private const int MaxNumberOfPersonsPerGraph = 100;

        private const string DefaultProviderName = "System.Data.ProdDb";

        /// <summary>
        /// List of all connections in graph.
        /// </summary>
        List<Relation> PersonConnections { get; set; }

        /// <summary>
        /// List of all nodes in graph.
        /// </summary>
        Dictionary<string, Person> Persons { get; set; }

        Queue<Person> ProcessingQueue { get; set; }

        /// <summary>
        /// Generating helper message from the time needed to generate the graph.
        /// </summary>
        TimeSpan GenerationTook { get; set; }

        /// <summary>
        /// Action which builds the home page.
        /// </summary>
        /// <param name="personViewModel">Input name from the main form.</param>
        /// <returns></returns>
        public ActionResult Index(PersonViewModel personViewModel)
        {
            Persons = new Dictionary<string, Person>();
            PersonConnections = new List<Relation>();
            ProcessingQueue = new Queue<Person>();
            ViewBag.PersonNames = Persons;
            ViewBag.PersonConnections = PersonConnections;
            ViewBag.Message = "Enter person name to start graph traversal.";

            if (string.IsNullOrEmpty(personViewModel.InputName))
            {
                // Nothing to do here. Waiting for user input.
                return View();
            }

            ViewBag.InputName = personViewModel.InputName;

            Person startingPerson = new Person((personViewModel.InputName.DenormalizeString()), 0);
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

            FillBirthAndDeathDates();

            GenerationTook = sw.Elapsed;

            ViewBag.PersonsJson = JsonConvert.SerializeObject(Persons, Formatting.Indented);
            ViewBag.PersonsConnectionsJson = JsonConvert.SerializeObject(PersonConnections, Formatting.Indented);


            ViewBag.PersonNames = Persons;
            ViewBag.PersonConnections = PersonConnections;
            ViewBag.GenerationTook = string.Format("{0}s for generating graph of {1} nodes.", GenerationTook.TotalSeconds, Persons.Count);
            return View();
        }
        

        /// <summary>
        /// Empty for now.
        /// </summary>
        /// <param name="inputNameModel"></param>
        /// <returns></returns>
        public ActionResult About(PersonViewModel personViewModel)
        {
            return View();
        }

        [HttpPost]
        public ActionResult UserInput(PersonViewModel personViewModel)
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
            using (var connection = new QC.SqlConnection(GetConnectionString(DefaultProviderName)))
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

                    command.Parameters.AddWithValue("@name", term.DenormalizeString() + "%");

                    QC.SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        autocompleteItems.Add(reader.GetString(0).NormalizeString());
                    }
                }
            }

            return Json(autocompleteItems.ToArray(), JsonRequestBehavior.AllowGet);
        }


        #region private_helper_methods
        /// <summary>
        /// Structure representing a node directly read from database.
        /// </summary>
        class GraphRelationRaw
        {
            public string Name { get; set; }
            public string From { get; set; }
            public string To { get; set; }
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
            using (var connection = new QC.SqlConnection(GetConnectionString(HomeController.DefaultProviderName)))
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
                            new GraphRelationRaw
                            {
                                From = reader.GetString(0),
                                Name = reader.GetString(1),
                                To = reader.GetString(2)
                            });
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
                    ProcessingQueue.Enqueue(personFrom);
                }

                if (!Persons.TryGetValue(branch.To, out personTo))
                {
                    personTo = new Person(branch.To, startingPerson.DistanceFromCenter + 1);
                    Persons.Add(personTo.Name, personTo);
                    isPersonToNew = true;
                    ProcessingQueue.Enqueue(personTo);
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

            while (ProcessingQueue.Count != 0 && Persons.Count < MaxNumberOfPersonsPerGraph)
            {
                if (aggressive && startingPerson.DistanceFromCenter < 3)
                {
                    BuildGraph(ProcessingQueue.Dequeue(), true, true, aggressive);
                }
                else if (aggressive && startingPerson.DistanceFromCenter < 4)
                {
                    BuildGraph(ProcessingQueue.Dequeue(), true, false, aggressive);
                }
                if (startingPerson.DistanceFromCenter < 2 && Persons.Count < 5 && ProcessingQueue.Count < 5)
                {
                    BuildGraph(ProcessingQueue.Dequeue(), true, false);
                }
                else
                {
                    BuildGraph(ProcessingQueue.Dequeue(), false, false);
                }
            }
        }

        void FillBirthAndDeathDates()
        {
            if (Persons.Count == 0)
            {
                // Nothing to do here.
                return;
            }

            // Craft query for all the persons.
            //
            using (var connection = new QC.SqlConnection(GetConnectionString(HomeController.DefaultProviderName)))
            {
                connection.Open();
                using (var command = new QC.SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = DT.CommandType.Text;
                    // This shouldn't be done like this.
                    command.CommandText =
                        "SELECT name, birth_date, death_date FROM persons WHERE name IN ("
                        + string.Join(", ", Persons.Keys.Select(p => "'" + p.Replace("'", "''") + "'").ToArray()) + ")";

                    QC.SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        Person person = Persons[reader.GetString(0)];

                        string birthDateStringRaw = null;
                        string deathDateStringRaw = null;

                        if (!reader.IsDBNull(1))
                        {
                            birthDateStringRaw = reader.GetString(1);
                        }

                        if (!reader.IsDBNull(2))
                        {
                            deathDateStringRaw = reader.GetString(2);
                        }

                        // TODO: Need better logic for handling BC dates.
                        Action<string, Action<DateTime>> fillDate = (inputRaw, setter) =>
                        {
                            if (inputRaw != null)
                            {
                                DateTime dt;

                                // Try to extract something useful from date strings.
                                if (DateTime.TryParse(inputRaw, out dt) || DateTime.TryParse(inputRaw.Replace("\"", ""), out dt))
                                {
                                    setter(dt);
                                }
                                else
                                {
                                    int year;
                                    if (Int32.TryParse(inputRaw.Replace("\"", ""), out year))
                                    {
                                        dt = new DateTime(year, 1, 1);
                                        setter(dt);
                                    }
                                }
                            }
                        };

                        fillDate(birthDateStringRaw, (dt) => person.BirthDate = dt);
                        fillDate(deathDateStringRaw, (dt) => person.DeathDate = dt);
                    }
                }
            }
        }
        #endregion

    }
}