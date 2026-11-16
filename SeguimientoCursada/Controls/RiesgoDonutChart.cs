using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace SeguimientoCursada.Controls;

/// <summary>
/// Gráfico de dona con la distribución de riesgo. Se anima automáticamente al
/// aparecer y cada vez que cambian los datos.
/// </summary>
public sealed class RiesgoDonutChart : GraphicsView
{
    private const string NombreAnimacion = "formar-dona";
    private const uint DuracionAnimacion = 700;

    public static readonly BindableProperty OptimosProperty = BindableProperty.Create(
        nameof(Optimos),
        typeof(int),
        typeof(RiesgoDonutChart),
        0,
        propertyChanged: OnDatosChanged);

    public static readonly BindableProperty EnRiesgoProperty = BindableProperty.Create(
        nameof(EnRiesgo),
        typeof(int),
        typeof(RiesgoDonutChart),
        0,
        propertyChanged: OnDatosChanged);

    public static readonly BindableProperty CriticosProperty = BindableProperty.Create(
        nameof(Criticos),
        typeof(int),
        typeof(RiesgoDonutChart),
        0,
        propertyChanged: OnDatosChanged);

    public static readonly BindableProperty TotalCentroProperty = BindableProperty.Create(
        nameof(TotalCentro),
        typeof(int),
        typeof(RiesgoDonutChart),
        0,
        propertyChanged: OnDatosChanged);

    private readonly RiesgoDonutDrawable _drawable;
    private double _progreso;
    private bool _animando;

    public RiesgoDonutChart()
    {
        _drawable = new RiesgoDonutDrawable(this);
        Drawable = _drawable;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => Invalidate();
    }

    public int Optimos
    {
        get => (int)GetValue(OptimosProperty);
        set => SetValue(OptimosProperty, value);
    }

    public int EnRiesgo
    {
        get => (int)GetValue(EnRiesgoProperty);
        set => SetValue(EnRiesgoProperty, value);
    }

    public int Criticos
    {
        get => (int)GetValue(CriticosProperty);
        set => SetValue(CriticosProperty, value);
    }

    public int TotalCentro
    {
        get => (int)GetValue(TotalCentroProperty);
        set => SetValue(TotalCentroProperty, value);
    }

    internal double Progreso => _progreso;

    /// <summary>
    /// Vuelve a reproducir la animación de formación de la dona. Si ya se está
    /// animando, no la reinicia.
    /// </summary>
    public void ReproducirAnimacion()
    {
        if (_animando)
        {
            return;
        }

        IniciarAnimacion();
    }

    internal Color ColorFondoDona => EsTemaOscuro
        ? Color.FromArgb("#3A3A3A")
        : Color.FromArgb("#E6E6E6");

    internal Color ColorTexto => EsTemaOscuro
        ? Colors.White
        : Color.FromArgb("#212121");

    private static bool EsTemaOscuro
    {
        get
        {
            var aplicacion = Application.Current;

            if (aplicacion is null)
            {
                return false;
            }

            var tema = aplicacion.UserAppTheme == AppTheme.Unspecified
                ? aplicacion.RequestedTheme
                : aplicacion.UserAppTheme;

            return tema == AppTheme.Dark;
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        IniciarAnimacion();
    }

    private static void OnDatosChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not RiesgoDonutChart grafico)
        {
            return;
        }

        if (grafico._animando || !grafico.IsLoaded)
        {
            grafico.Invalidate();
            return;
        }

        grafico.IniciarAnimacion();
    }

    private void IniciarAnimacion()
    {
        this.AbortAnimation(NombreAnimacion);
        _progreso = 0;
        _animando = true;
        Invalidate();

        this.Animate(
            NombreAnimacion,
            valor =>
            {
                _progreso = valor;
                Invalidate();
            },
            start: 0,
            end: 1,
            rate: 16,
            length: DuracionAnimacion,
            easing: Easing.CubicOut,
            finished: (_, _) =>
            {
                _progreso = 1;
                _animando = false;
                Invalidate();
            });
    }

    private sealed class RiesgoDonutDrawable : IDrawable
    {
        private readonly RiesgoDonutChart _grafico;

        public RiesgoDonutDrawable(RiesgoDonutChart grafico)
        {
            _grafico = grafico;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var valores = new[] { _grafico.Optimos, _grafico.EnRiesgo, _grafico.Criticos };
            var total = valores[0] + valores[1] + valores[2];
            var tamano = Math.Min(dirtyRect.Width, dirtyRect.Height);

            if (tamano <= 0)
            {
                return;
            }

            var grosor = Math.Max(14f, tamano * 0.12f);
            var radio = (tamano - grosor) / 2f - 4f;

            if (radio <= 0)
            {
                return;
            }

            var centro = new PointF(dirtyRect.Center.X, dirtyRect.Center.Y);
            var x = centro.X - radio;
            var y = centro.Y - radio;
            var diametro = radio * 2f;

            canvas.StrokeLineCap = LineCap.Butt;
            canvas.StrokeSize = grosor;
            canvas.StrokeColor = _grafico.ColorFondoDona;
            canvas.DrawEllipse(x, y, diametro, diametro);

            if (total <= 0)
            {
                return;
            }

            var colores = new[] { RiesgoColores.Optimo, RiesgoColores.EnRiesgo, RiesgoColores.Critico };
            var barridoAnimado = 360f * (float)Math.Clamp(_grafico.Progreso, 0, 1);
            var acumulado = 0f;

            for (var i = 0; i < valores.Length; i++)
            {
                if (valores[i] <= 0)
                {
                    continue;
                }

                var porcion = 360f * valores[i] / total;
                var inicio = acumulado;
                var fin = Math.Min(acumulado + porcion, barridoAnimado);

                if (fin > inicio)
                {
                    canvas.StrokeColor = colores[i];

                    if (fin - inicio >= 359.9f)
                    {
                        canvas.DrawEllipse(x, y, diametro, diametro);
                    }
                    else
                    {
                        DibujarArco(canvas, centro, radio, -90f + inicio, -90f + fin);
                    }
                }

                acumulado += porcion;

                if (acumulado >= barridoAnimado)
                {
                    break;
                }
            }

            DibujarCentro(canvas, centro, radio, total);
        }

        /// <summary>
        /// Dibuja un arco trazando puntos sobre la circunferencia. Se evita
        /// ICanvas.DrawArc porque en Windows interpreta el barrido al revés y
        /// las porciones quedan con el tamaño invertido.
        /// </summary>
        private static void DibujarArco(
            ICanvas canvas,
            PointF centro,
            float radio,
            float desdeGrados,
            float hastaGrados)
        {
            var recorrido = hastaGrados - desdeGrados;

            if (recorrido <= 0)
            {
                return;
            }

            var pasos = Math.Max(1, (int)MathF.Ceiling(recorrido));
            var path = new PathF();

            for (var paso = 0; paso <= pasos; paso++)
            {
                var angulo = desdeGrados + recorrido * paso / pasos;
                var radianes = angulo * MathF.PI / 180f;
                var punto = new PointF(
                    centro.X + radio * MathF.Cos(radianes),
                    centro.Y + radio * MathF.Sin(radianes));

                if (paso == 0)
                {
                    path.MoveTo(punto);
                }
                else
                {
                    path.LineTo(punto);
                }
            }

            canvas.DrawPath(path);
        }

        private void DibujarCentro(ICanvas canvas, PointF centro, float radio, int total)
        {
            var fuenteNumero = radio * 0.5f;
            var fuenteEtiqueta = Math.Max(9f, radio * 0.16f);
            var altoNumero = fuenteNumero * 1.2f;
            var altoEtiqueta = fuenteEtiqueta * 1.4f;
            var altoTotal = altoNumero + altoEtiqueta;
            var y = centro.Y - altoTotal / 2f;
            var ancho = radio * 2f;
            var x = centro.X - radio;

            canvas.FontColor = _grafico.ColorTexto;
            canvas.FontSize = fuenteNumero;
            canvas.DrawString(
                total.ToString(CultureInfo.InvariantCulture),
                x,
                y,
                ancho,
                altoNumero,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            canvas.FontSize = fuenteEtiqueta;
            canvas.DrawString(
                "con datos",
                x,
                y + altoNumero,
                ancho,
                altoEtiqueta,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }
}
