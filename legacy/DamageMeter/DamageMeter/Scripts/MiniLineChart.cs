using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DamageMeter.Scripts;

public partial class MiniLineChart : Control
{
	private readonly List<int> _values = new List<int>();

	private Color _lineColor = Colors.White;

	public void SetSeries(IEnumerable<int> values, Color lineColor)
	{
		_values.Clear();
		_values.AddRange(values);
		_lineColor = lineColor;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		DrawRect(new Rect2(Vector2.Zero, size), new Color(0.03f, 0.04f, 0.08f, 0.75f), true);
		DrawRect(new Rect2(Vector2.Zero, size), new Color(_lineColor.R, _lineColor.G, _lineColor.B, 0.16f), false, 1f);

		float left = 8f;
		float right = Math.Max(left + 1f, size.X - 8f);
		float top = 8f;
		float bottom = Math.Max(top + 1f, size.Y - 8f);
		Color gridColor = new Color(1f, 1f, 1f, 0.06f);
		for (int i = 0; i < 3; i++)
		{
			float y = Mathf.Lerp(top, bottom, i / 2f);
			DrawLine(new Vector2(left, y), new Vector2(right, y), gridColor, 1f, true);
		}

		if (_values.Count < 2 || _values.All((int value) => value <= 0))
		{
			return;
		}

		int maxValue = Math.Max(1, _values.Max());
		Vector2? vector = null;
		for (int j = 0; j < _values.Count; j++)
		{
			float weight = (_values.Count <= 1) ? 0f : (float)j / (float)(_values.Count - 1);
			float x = Mathf.Lerp(left, right, weight);
			float normalized = (float)_values[j] / (float)maxValue;
			float y2 = Mathf.Lerp(bottom, top, normalized);
			Vector2 vector2 = new Vector2(x, y2);
			if (vector.HasValue)
			{
				DrawLine(vector.Value, vector2, _lineColor, 2.25f, true);
			}
			DrawCircle(vector2, 2.5f, new Color(_lineColor.R, _lineColor.G, _lineColor.B, 0.95f));
			vector = vector2;
		}
	}
}
