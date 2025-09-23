using System.Linq.Expressions;

namespace GiselX.Common.Extensions;

public static class QueryableExtensions
{
    public static IOrderedQueryable<TSource> OrderBy<TSource>(
        this IQueryable<TSource> query, string propertyName)
    {
        var entityType = typeof(TSource);

        //Create x=>x.PropName
        var propertyInfo = entityType.GetProperty(propertyName);
        var arg = Expression.Parameter(entityType, "x");
        var property = Expression.Property(arg, propertyName);
        var selector = Expression.Lambda(property, arg);

        //Get System.Linq.Queryable.OrderBy() method.
        var enumerableType = typeof(Queryable);
        var method = enumerableType.GetMethods()
            .Where(m => m is { Name: "OrderBy", IsGenericMethodDefinition: true })
            .Where(m =>
            {
                var parameters = m.GetParameters().ToList();
                //Put more restriction here to ensure selecting the right overload                
                return parameters.Count == 2;//overload that has 2 parameters
            }).Single();
        //The linq's OrderBy<TSource, TKey> has two generic types, which provided here
        
        if (propertyInfo?.PropertyType == null) throw new ArgumentException("Property not found");
        
        var genericMethod = method.MakeGenericMethod(entityType, propertyInfo.PropertyType);

        /*Call query.OrderBy(selector), with query and selector: x=> x.PropName
              Note that we pass the selector as Expression to the method and we don't compile it.
              By doing so EF can extract "order by" columns and generate SQL for it.*/
        if (genericMethod.Invoke(genericMethod, [query, selector]) is IOrderedQueryable<TSource> newQuery) return newQuery;
        throw new Exception("Failed to generate order by");
    }
}