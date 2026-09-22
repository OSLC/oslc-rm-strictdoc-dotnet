using System.Collections;
using System.Globalization;
using System.Reflection;
using OSLC4Net.Core.Attribute;
using OSLC4Net.Core.Query;
using OSLC4Net.Domains.RequirementsManagement;

namespace StrictDocOslcRm.Services;

/// <summary>
/// Result of applying an OSLC Query to an in-memory set of requirements.
/// </summary>
/// <param name="Members">The members on the requested page, after filtering, sorting and paging.</param>
/// <param name="TotalCount">The total number of members matching the query across all pages.</param>
/// <param name="SelectedProperties">
/// The property filter map (from <c>oslc.select</c>) suitable for OSLC4Net RDF serialization.
/// </param>
/// <param name="NextPage">The next-page URI when the result is paged, otherwise <c>null</c>.</param>
public sealed record OslcQueryOutcome(
    IReadOnlyList<Requirement> Members,
    int TotalCount,
    IDictionary<string, object> SelectedProperties,
    string? NextPage);

/// <summary>
/// Thrown when an OSLC Query expression is syntactically invalid (maps to HTTP 400).
/// </summary>
public sealed class OslcQueryBadRequestException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Thrown when an OSLC Query expression uses a construct this server does not implement
/// (maps to HTTP 501).
/// </summary>
public sealed class OslcQueryNotImplementedException(string message) : Exception(message);

/// <summary>
/// Evaluates OSLC Query parameters (<c>oslc.prefix</c>, <c>oslc.where</c>, <c>oslc.select</c>,
/// <c>oslc.orderBy</c>, <c>oslc.searchTerms</c>, <c>oslc.pageSize</c>) against
/// <see cref="Requirement"/> instances, entirely in memory.
/// </summary>
public interface IOslcQueryService
{
    /// <summary>
    /// Apply OSLC Query parameters to <paramref name="source"/>.
    /// </summary>
    /// <param name="nextPageUriFactory">
    /// Produces the absolute URI for the given 1-based page number, used to advertise the next page.
    /// </param>
    OslcQueryOutcome Apply(
        IReadOnlyList<Requirement> source,
        string? prefix,
        string? where,
        string? select,
        string? orderBy,
        string? searchTerms,
        int? pageSize,
        int page,
        Func<int, string> nextPageUriFactory);
}

public sealed class OslcQueryService : IOslcQueryService
{
    private const string RdfType = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
    private const string RequirementType = "http://open-services.net/ns/rm#Requirement";

    private static readonly IReadOnlyDictionary<string, string> DefaultPrefixes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dcterms"] = "http://purl.org/dc/terms/",
            ["oslc"] = "http://open-services.net/ns/core#",
            ["oslc_rm"] = "http://open-services.net/ns/rm#",
            ["rdf"] = "http://www.w3.org/1999/02/22-rdf-syntax-ns#",
            ["rdfs"] = "http://www.w3.org/2000/01/rdf-schema#",
            ["xsd"] = "http://www.w3.org/2001/XMLSchema#",
        };

    // Maps an OSLC property-definition URI to the CLR accessor on Requirement.
    private static readonly IReadOnlyDictionary<string, PropertyInfo> Accessors = BuildAccessors();

    private static Dictionary<string, PropertyInfo> BuildAccessors()
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        foreach (var property in typeof(Requirement).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var definition = property.GetCustomAttribute<OslcPropertyDefinition>();
            if (definition is not null && !map.ContainsKey(definition.value))
            {
                map[definition.value] = property;
            }
        }

        return map;
    }

    public OslcQueryOutcome Apply(
        IReadOnlyList<Requirement> source,
        string? prefix,
        string? where,
        string? select,
        string? orderBy,
        string? searchTerms,
        int? pageSize,
        int page,
        Func<int, string> nextPageUriFactory)
    {
        var prefixMap = BuildPrefixMap(prefix);

        IEnumerable<Requirement> query = source;

        if (!string.IsNullOrWhiteSpace(where))
        {
            var whereClause = ParseWhere(where, prefixMap);
            query = query.Where(requirement => MatchesAll(requirement, whereClause.Children));
        }

        if (!string.IsNullOrWhiteSpace(searchTerms))
        {
            var terms = ParseSearchTerms(searchTerms);
            if (terms.Count > 0)
            {
                query = query.Where(requirement => MatchesSearchTerms(requirement, terms));
            }
        }

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            query = ApplyOrderBy(query, ParseOrderBy(orderBy, prefixMap));
        }

        var matched = query.ToList();
        var totalCount = matched.Count;

        var pageNumber = page < 1 ? 1 : page;
        string? nextPage = null;
        IReadOnlyList<Requirement> members = matched;

        if (pageSize is > 0)
        {
            var size = pageSize.Value;
            members = matched.Skip((pageNumber - 1) * size).Take(size).ToList();
            if (pageNumber * size < totalCount)
            {
                nextPage = nextPageUriFactory(pageNumber + 1);
            }
        }

        var selectedProperties = BuildSelectedProperties(select, prefixMap);

        return new OslcQueryOutcome(members, totalCount, selectedProperties, nextPage);
    }

    private static IDictionary<string, string> BuildPrefixMap(string? prefix)
    {
        var map = new Dictionary<string, string>(DefaultPrefixes, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return map;
        }

        try
        {
            foreach (var entry in QueryUtils.ParsePrefixes(prefix))
            {
                map[entry.Key] = entry.Value;
            }
        }
        catch (ParseException exception)
        {
            throw new OslcQueryBadRequestException($"Invalid oslc.prefix: {exception.Message}", exception);
        }

        return map;
    }

    private static WhereClause ParseWhere(string where, IDictionary<string, string> prefixMap)
    {
        try
        {
            return QueryUtils.ParseWhere(where, prefixMap);
        }
        catch (ParseException exception)
        {
            throw new OslcQueryBadRequestException($"Invalid oslc.where: {exception.Message}", exception);
        }
    }

    private static OrderByClause ParseOrderBy(string orderBy, IDictionary<string, string> prefixMap)
    {
        try
        {
            return QueryUtils.ParseOrderBy(orderBy, prefixMap);
        }
        catch (ParseException exception)
        {
            throw new OslcQueryBadRequestException($"Invalid oslc.orderBy: {exception.Message}", exception);
        }
    }

    private static IList<string> ParseSearchTerms(string searchTerms)
    {
        try
        {
            return QueryUtils.ParseSearchTerms(searchTerms);
        }
        catch (ParseException exception)
        {
            throw new OslcQueryBadRequestException($"Invalid oslc.searchTerms: {exception.Message}", exception);
        }
    }

    private static IDictionary<string, object> BuildSelectedProperties(
        string? select,
        IDictionary<string, string> prefixMap)
    {
        // With no oslc.select, OSLC returns the full representation; "*" yields a wildcard map so
        // every property is serialized. An empty plain map would otherwise suppress all properties.
        var expression = string.IsNullOrWhiteSpace(select) ? "*" : select;
        try
        {
            var selectClause = QueryUtils.ParseSelect(expression, prefixMap);
            return QueryUtils.InvertSelectedProperties(selectClause);
        }
        catch (ParseException exception)
        {
            throw new OslcQueryBadRequestException($"Invalid oslc.select: {exception.Message}", exception);
        }
    }

    private static bool MatchesSearchTerms(Requirement requirement, IList<string> terms)
    {
        var haystack = $"{requirement.Title}\n{requirement.Description}\n{requirement.Identifier}";
        return terms.Any(term =>
            haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAll(Requirement requirement, IEnumerable<SimpleTerm> terms)
    {
        return terms.All(term => MatchesTerm(requirement, term));
    }

    private static bool MatchesTerm(Requirement requirement, SimpleTerm term)
    {
        var property = term.Property;
        if (property is null)
        {
            throw new OslcQueryNotImplementedException(
                "Nested oslc.where terms are not supported by this server.");
        }

        var propertyUri = property.ns + property.local;
        var cells = GetCells(requirement, propertyUri).ToList();

        switch (term)
        {
            case InTerm inTerm:
                return inTerm.Values.Any(value => cells.Any(cell => ScalarEquals(cell, value)));

            case ComparisonTerm comparison:
                var operand = comparison.Operand;
                if (comparison.Operator == Operator.NOT_EQUALS)
                {
                    return !cells.Any(cell => ScalarEquals(cell, operand));
                }

                if (comparison.Operator == Operator.EQUALS)
                {
                    return cells.Any(cell => ScalarEquals(cell, operand));
                }

                return cells.Any(cell => Ordered(cell, operand, comparison.Operator));

            default:
                throw new OslcQueryNotImplementedException(
                    $"oslc.where term type {term.Type} is not supported by this server.");
        }
    }

    private static IEnumerable<object> GetCells(Requirement requirement, string propertyUri)
    {
        if (string.Equals(propertyUri, RdfType, StringComparison.Ordinal))
        {
            yield return new Uri(RequirementType);
            yield break;
        }

        if (!Accessors.TryGetValue(propertyUri, out var accessor))
        {
            yield break;
        }

        var value = accessor.GetValue(requirement);
        switch (value)
        {
            case null:
                yield break;
            case string text:
                yield return text;
                break;
            case Uri uri:
                yield return uri;
                break;
            case DateTimeOffset dateTimeOffset:
                yield return dateTimeOffset;
                break;
            case DateTime dateTime:
                yield return new DateTimeOffset(dateTime);
                break;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    if (item is not null)
                    {
                        yield return item;
                    }
                }

                break;
            default:
                yield return value;
                break;
        }
    }

    private static bool ScalarEquals(object cell, Value operand)
    {
        var (cellText, cellIsUri, cellNumber, cellDate) = Describe(cell);
        var (operandText, operandIsUri, operandNumber, operandDate) = Describe(operand);

        if (cellIsUri || operandIsUri)
        {
            return string.Equals(cellText, operandText, StringComparison.Ordinal);
        }

        if (cellNumber is not null && operandNumber is not null)
        {
            return cellNumber.Value == operandNumber.Value;
        }

        if (cellDate is not null && operandDate is not null)
        {
            return cellDate.Value == operandDate.Value;
        }

        return string.Equals(cellText, operandText, StringComparison.Ordinal);
    }

    private static bool Ordered(object cell, Value operand, Operator op)
    {
        var (cellText, _, cellNumber, cellDate) = Describe(cell);
        var (operandText, _, operandNumber, operandDate) = Describe(operand);

        int comparison;
        if (cellNumber is not null && operandNumber is not null)
        {
            comparison = cellNumber.Value.CompareTo(operandNumber.Value);
        }
        else if (cellDate is not null && operandDate is not null)
        {
            comparison = cellDate.Value.CompareTo(operandDate.Value);
        }
        else
        {
            comparison = string.CompareOrdinal(cellText, operandText);
        }

        return op switch
        {
            Operator.LESS_THAN => comparison < 0,
            Operator.GREATER_THAN => comparison > 0,
            Operator.LESS_EQUALS => comparison <= 0,
            Operator.GREATER_EQUALS => comparison >= 0,
            _ => false,
        };
    }

    private static (string Text, bool IsUri, decimal? Number, DateTimeOffset? Date) Describe(object cell)
    {
        switch (cell)
        {
            case Uri uri:
                return (uri.ToString(), true, null, null);
            case DateTimeOffset dateTimeOffset:
                return (dateTimeOffset.ToString("O", CultureInfo.InvariantCulture), false, null, dateTimeOffset);
            case bool boolean:
                return (boolean ? "true" : "false", false, null, null);
            case string text:
                return (text, false, null, null);
            default:
                var formatted = Convert.ToString(cell, CultureInfo.InvariantCulture) ?? string.Empty;
                return decimal.TryParse(formatted, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                    ? (formatted, false, number, null)
                    : (formatted, false, null, null);
        }
    }

    private static (string Text, bool IsUri, decimal? Number, DateTimeOffset? Date) Describe(Value operand)
    {
        switch (operand)
        {
            case UriRefValue uri:
                return (uri.Value, true, null, null);
            case BooleanValue boolean:
                return (boolean.Value ? "true" : "false", false, null, null);
            case DecimalValue dec:
                return decimal.TryParse(dec.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                    ? (dec.Value, false, number, null)
                    : (dec.Value, false, null, null);
            case LangedStringValue langed:
                return (langed.Value, false, null, null);
            case TypedValue typed:
                return DescribeTyped(typed);
            case StringValue str:
                return (str.Value, false, null, null);
            default:
                return (operand.ToString() ?? string.Empty, false, null, null);
        }
    }

    private static (string Text, bool IsUri, decimal? Number, DateTimeOffset? Date) DescribeTyped(TypedValue typed)
    {
        var datatype = typed.PrefixedName?.local ?? string.Empty;
        if (datatype is "dateTime" or "date" &&
            DateTimeOffset.TryParse(typed.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            return (typed.Value, false, null, date);
        }

        if (datatype is "integer" or "decimal" or "double" or "float" or "long" or "int" &&
            decimal.TryParse(typed.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
        {
            return (typed.Value, false, number, null);
        }

        return (typed.Value, false, null, null);
    }

    private static IEnumerable<Requirement> ApplyOrderBy(IEnumerable<Requirement> query, SortTerms sortTerms)
    {
        IOrderedEnumerable<Requirement>? ordered = null;
        foreach (var term in sortTerms.Children)
        {
            if (term is not SimpleSortTerm simple || simple.Identifier is null)
            {
                throw new OslcQueryNotImplementedException(
                    "Scoped oslc.orderBy terms are not supported by this server.");
            }

            var propertyUri = simple.Identifier.ns + simple.Identifier.local;
            string KeySelector(Requirement requirement) =>
                GetCells(requirement, propertyUri)
                    .Select(cell => Describe(cell).Text)
                    .FirstOrDefault() ?? string.Empty;

            if (ordered is null)
            {
                ordered = simple.Ascending
                    ? query.OrderBy(KeySelector, StringComparer.Ordinal)
                    : query.OrderByDescending(KeySelector, StringComparer.Ordinal);
            }
            else
            {
                ordered = simple.Ascending
                    ? ordered.ThenBy(KeySelector, StringComparer.Ordinal)
                    : ordered.ThenByDescending(KeySelector, StringComparer.Ordinal);
            }
        }

        return ordered ?? query;
    }
}
