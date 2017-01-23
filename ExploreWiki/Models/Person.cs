using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ExploreWiki.Helpers;

namespace ExploreWiki.Models
{
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
        public string NormalizedName { get { return Name.Normalize(); } }

        /// <summary>
        /// Optional birth date.
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// Optional death date.
        /// </summary>
        public DateTime? DeathDate { get; set; }

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
                    return "rgb(255, 0, 0)";
                }
                else
                {
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
}