from urllib.parse import urlparse, urlsplit

affected_by = "http://open-services.net/ns/rm#affectedBy"
tracked_by = "http://open-services.net/ns/rm#trackedBy"

parse_a = urlparse(affected_by)
parse_b = urlparse(tracked_by)
split_a = urlsplit(affected_by)
split_b = urlsplit(tracked_by)

print("Python urllib.parse.urlparse")
print(f"type={type(parse_a).__module__}.{type(parse_a).__name__}")
print(f"a={parse_a.geturl()} fragment={parse_a.fragment}")
print(f"b={parse_b.geturl()} fragment={parse_b.fragment}")
print(f"a == b={parse_a == parse_b}")
print(f"hash(a)={hash(parse_a)} hash(b)={hash(parse_b)}")
print(f"set(ParseResult).count={len({parse_a, parse_b})}")

print()
print("Python urllib.parse.urlsplit")
print(f"type={type(split_a).__module__}.{type(split_a).__name__}")
print(f"a={split_a.geturl()} fragment={split_a.fragment}")
print(f"b={split_b.geturl()} fragment={split_b.fragment}")
print(f"a == b={split_a == split_b}")
print(f"hash(a)={hash(split_a)} hash(b)={hash(split_b)}")
print(f"set(SplitResult).count={len({split_a, split_b})}")
print(f"set(str).count={len({affected_by, tracked_by})}")

