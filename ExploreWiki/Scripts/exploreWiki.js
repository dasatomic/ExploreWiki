
$(document).ready(function () {
    var canvas = document.getElementsByTagName('canvas')[0];
    canvas.height = 800;
    canvas.style.width = '800px';
    canvas.style.height = '800px';

    var networkContainer = document.getElementById('mynetwork');

    // TO DO populating DATA object: nodes/edges can (and should) be populated on CLIENT(not on Server side)
    // Reason: better perfomance
    // The BEST way: 
    // API Controler for feching data which will return JsonResult. JsonResult, fatched with AJAX calls sould be then deserialized (PersonNames => nodes & PersonConnections => edges) and GRAPH should be generated on CLIENT side   
    // The EASIER way: 
    // Existing controller action can pass serialized objects (ViewBag.PersonsJson & ViewBag.PersonsConnectionsJson) to View which will be pased as HTML data-attributes to client side and GRAPH should be generated on CLIENT side  

    //Example:
    var personNamesJson = $("#networkContainer").attr("data-personNamesJson");
    var personConnectionsJson = $("#networkContainer").attr("data-personsConnectionsJson");
    var searchTermsViaAttribute = $("#networkContainer").attr("data-searchTerm");

    // TO DO create an array with nodes from serialized JSONs on CLIENT side...
    // var nodes = new vis.DataSet([]);
    // create an array with edges
    // var edges = new vis.DataSet([]);


    var data = {
        nodes: nodes,
        edges: edges
    };

    var networkOptions = {
        // layout: {
        //     hierarchical: {
        //         sortMethod: "directed"
        //     }
        // },
        physics: {
            enabled: true,
            "barnesHut": {
                "gravitationalConstant": -10,
                "centralGravity": 0.0,
                "damping": 1,
                "avoidOverlap": 1,
                "springLength": 450
                //,"nodeDistance": 400
            },
            "maxVelocity": 0.5,
            "minVelocity": 0,
            "timestep": 0.01,
            repulsion: {
                centralGravity: 0.0,
                springLength: 500,
                springConstant: 0.05,
                nodeDistance: 500,
                damping: 0.09
            },
            stabilization: {
                enabled: true,
                iterations: 100,
                updateInterval: 0.01,
                onlyDynamicEdges: false,
                fit: true
            },
        },
        layout: { improvedLayout: true }
    };
    // Create a Network   
    var network = new vis.Network(networkContainer, data, networkOptions);

    var timelineContainer = document.getElementById('timeline');
    var timelineOptions = {
        //type: 'point',
        //showMajorLabels: false
    };
    // Create a Timeline
    var timeline = new vis.Timeline(timelineContainer, items, timelineOptions);
    

    // TODO: wiki links point to local domain. This should be changed here.
    // Either redirect to wiki or parhaps do search on explorewiki.
    var url = "http://en.wikipedia.org/w/api.php?action=parse&format=json&page=" + searchTerm + "&redirects&prop=text&callback=?";
    $.getJSON(url,
        function (data) {
            var wikiHTML = "";
            if (data.error === undefined)
                wikiHTML = data.parse.text["*"];
            $wikiDOM = $("<document>" + wikiHTML + "</document>");
            $("#wikiboxshow").html($wikiDOM.find('.infobox').html());
        });

    network.on("click",
        function (params) {
            var node = nodes.get(params.nodes[0]);
            var searchTerm = node.label;
            var url = "http://en.wikipedia.org/w/api.php?action=parse&format=json&page=" +
                searchTerm + "&redirects&prop=text&callback=?";
            $.getJSON(url,
                function (data) {
                    var wikiHTML = "";
                    if (data.error === undefined)
                        wikiHTML = data.parse.text["*"];
                    $wikiDOM = $("<document>" + wikiHTML + "</document>");
                    $("#wikiboxshow").html($wikiDOM.find('.infobox').html());
                });
        });
});