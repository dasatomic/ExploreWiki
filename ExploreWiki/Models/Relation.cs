using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ExploreWiki.Models
{
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
}