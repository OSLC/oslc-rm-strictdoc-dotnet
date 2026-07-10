use std::collections::HashSet;
use url::Url;

fn main() {
    let affected_by = "http://open-services.net/ns/rm#affectedBy";
    let tracked_by = "http://open-services.net/ns/rm#trackedBy";

    let url_a = Url::parse(affected_by).unwrap();
    let url_b = Url::parse(tracked_by).unwrap();

    println!("Rust url::Url");
    println!("type=url::Url");
    println!("a={} fragment={:?}", url_a.as_str(), url_a.fragment());
    println!("b={} fragment={:?}", url_b.as_str(), url_b.fragment());
    println!("a == b={}", url_a == url_b);

    let url_set = HashSet::from([url_a.clone(), url_b.clone()]);
    let string_set = HashSet::from([affected_by.to_string(), tracked_by.to_string()]);
    println!("HashSet<Url>.count={}", url_set.len());
    println!("HashSet<String>.count={}", string_set.len());
}

