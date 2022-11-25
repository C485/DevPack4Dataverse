public static class PredicateBuilder
{
	public static Expression<Func<T, bool>> Or<T>(
		this Expression<Func<T, bool>> expr1,
		Expression<Func<T, bool>> expr2)
	{
		ParameterExpression parameter = Expression.Parameter(typeof(T));

		ReplaceExpressionVisitor leftVisitor = new(expr1.Parameters[0], parameter);
		Expression left = leftVisitor.Visit(expr1.Body);

		ReplaceExpressionVisitor rightVisitor = new(expr2.Parameters[0], parameter);
		Expression right = rightVisitor.Visit(expr2.Body);

		return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left, right), parameter);
	}

	public static Expression<Func<T, bool>> And<T>(
		this Expression<Func<T, bool>> expr1,
		Expression<Func<T, bool>> expr2)
	{
		ParameterExpression parameter = Expression.Parameter(typeof(T));

		ReplaceExpressionVisitor leftVisitor = new(expr1.Parameters[0], parameter);
		Expression left = leftVisitor.Visit(expr1.Body);

		ReplaceExpressionVisitor rightVisitor = new(expr2.Parameters[0], parameter);
		Expression right = rightVisitor.Visit(expr2.Body);

		return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left, right), parameter);
	}

	public static Expression<Func<T, bool>> Empty<T>()
	{
		return _ => false;
	}

	private class ReplaceExpressionVisitor
		: ExpressionVisitor
	{
		private readonly Expression _newValue;
		private readonly Expression _oldValue;

		public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
		{
			_oldValue = oldValue;
			_newValue = newValue;
		}

		public override Expression Visit(Expression node)
		{
			return node == _oldValue ? _newValue : base.Visit(node);
		}
	}
}
