import org.apache.jena.graph.Node;
import org.apache.jena.graph.NodeFactory;
import org.apache.jena.rdf.model.Model;
import org.apache.jena.rdf.model.ModelFactory;
import org.apache.jena.rdf.model.Property;
import org.apache.jena.rdf.model.Resource;
import org.apache.jena.rdf.model.ResourceFactory;

import java.net.URI;
import java.net.URL;
import java.util.HashSet;

public class UriFragmentProbe {
    public static void main(String[] args) throws Exception {
        String affectedBy = "http://open-services.net/ns/rm#affectedBy";
        String trackedBy = "http://open-services.net/ns/rm#trackedBy";

        URI uriA = new URI(affectedBy);
        URI uriB = new URI(trackedBy);
        System.out.println("Java java.net.URI");
        System.out.println("type=" + uriA.getClass().getName());
        System.out.println("a=" + uriA + " fragment=" + uriA.getFragment());
        System.out.println("b=" + uriB + " fragment=" + uriB.getFragment());
        System.out.println("a.equals(b)=" + uriA.equals(uriB));
        System.out.println("hash(a)=" + uriA.hashCode() + " hash(b)=" + uriB.hashCode());
        System.out.println("HashSet<URI>.Count=" + new HashSet<>(java.util.List.of(uriA, uriB)).size());

        URL urlA = uriA.toURL();
        URL urlB = uriB.toURL();
        System.out.println();
        System.out.println("Java java.net.URL");
        System.out.println("type=" + urlA.getClass().getName());
        System.out.println("a=" + urlA + " ref=" + urlA.getRef());
        System.out.println("b=" + urlB + " ref=" + urlB.getRef());
        System.out.println("a.equals(b)=" + urlA.equals(urlB));
        System.out.println("hash(a)=" + urlA.hashCode() + " hash(b)=" + urlB.hashCode());
        System.out.println("HashSet<URL>.Count=" + new HashSet<>(java.util.List.of(urlA, urlB)).size());

        Node nodeA = NodeFactory.createURI(affectedBy);
        Node nodeB = NodeFactory.createURI(trackedBy);
        System.out.println();
        System.out.println("Apache Jena Node");
        System.out.println("node type=" + nodeA.getClass().getName());
        System.out.println("native URI value type=" + nodeA.getURI().getClass().getName());
        System.out.println("nodeA.getURI()=" + nodeA.getURI());
        System.out.println("nodeB.getURI()=" + nodeB.getURI());
        System.out.println("nodeA.equals(nodeB)=" + nodeA.equals(nodeB));
        System.out.println("HashSet<Node>.Count=" + new HashSet<>(java.util.List.of(nodeA, nodeB)).size());

        Model model = ModelFactory.createDefaultModel();
        Resource subject = ResourceFactory.createResource("http://example.com/s");
        Resource object = ResourceFactory.createResource("http://example.com/o");
        Property propertyA = ResourceFactory.createProperty(affectedBy);
        Property propertyB = ResourceFactory.createProperty(trackedBy);
        model.add(subject, propertyA, object);
        model.add(subject, propertyB, object);
        System.out.println("Model statement count after two fragment-distinct predicates=" + model.size());
        System.out.println("StatementsWithPredicate(affectedBy)=" + model.listStatements(null, propertyA, (Resource) null).toList().size());
        System.out.println("StatementsWithPredicate(trackedBy)=" + model.listStatements(null, propertyB, (Resource) null).toList().size());
    }
}
