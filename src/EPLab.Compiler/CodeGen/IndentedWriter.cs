using System.Text;

namespace EPLab.Compiler.CodeGen;

internal sealed class IndentedWriter
{
    private readonly StringBuilder builder = new();
    private int indentation;
    private bool atLineStart = true;

    public void Write(string value)
    {
        if (atLineStart && value.Length > 0)
        {
            builder.Append(' ', indentation * 4);
            atLineStart = false;
        }
        builder.Append(value);
    }

    public void WriteLine(string value = "")
    {
        if (value.Length > 0)
            Write(value);
        builder.AppendLine();
        atLineStart = true;
    }

    public IDisposable Block(string header)
    {
        WriteLine(header);
        WriteLine("{");
        indentation++;
        return new BlockScope(this);
    }

    public void Indent() => indentation++;

    public void Unindent() => indentation = Math.Max(0, indentation - 1);

    public override string ToString() => builder.ToString();

    private sealed class BlockScope : IDisposable
    {
        private readonly IndentedWriter writer;
        private bool disposed;

        public BlockScope(IndentedWriter writer) => this.writer = writer;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            writer.Unindent();
            writer.WriteLine("}");
        }
    }
}
