#nullable enable
using System.Globalization;
using TrabalhoM1.Grafos;

namespace TrabalhoM3;

internal static class Program
{
    private const int LimiteImpressaoArestas = 40;

    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        if (args is ["--test"]) return Testes.Executar();
        if (args.Length is 1 or 3) return ExecutarDireto(args);
        if (args.Length > 0)
        {
            Console.WriteLine("Uso: dotnet run [-- --test | -- <arquivo> [origem destino]]");
            return 1;
        }
        Console.WriteLine("=== TRABALHO 3.1 — FLUXO MÁXIMO E BUSCA LOCAL ===");
        while (true)
        {
            var arquivos = LocalizarArquivos();
            Console.WriteLine("\nArquivos disponíveis:");
            for (int i = 0; i < arquivos.Count; i++)
                Console.WriteLine($"{i + 1}) {Path.GetRelativePath(Directory.GetCurrentDirectory(), arquivos[i])}");
            Console.WriteLine("C) Informar caminho\n0) Sair");
            Console.Write("Arquivo: ");
            string? entrada = Console.ReadLine();
            if (entrada is null or "0") return 0;
            string caminho;
            if (entrada.Equals("c", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Caminho do arquivo: ");
                caminho = (Console.ReadLine() ?? "").Trim().Trim('"');
            }
            else if (int.TryParse(entrada, out int indice) && indice >= 1 && indice <= arquivos.Count)
                caminho = arquivos[indice - 1];
            else { Console.WriteLine("Opção inválida."); continue; }

            Console.WriteLine("Representação: 1) Lista (padrão)  2) Matriz");
            Console.Write("Escolha [1]: ");
            string? representacao = Console.ReadLine();
            if (representacao is null) return 0;
            if (representacao is not ("" or "1" or "2"))
            { Console.WriteLine("Representação inválida."); continue; }
            Grafos grafo = representacao == "2" ? new GrafoMatriz(true, true) : new GrafoLista(true, true);
            if (!grafo.carregarDoArquivo(caminho))
            {
                Console.WriteLine("Não foi possível carregar o arquivo. Cabeçalho esperado: V A D P.");
                continue;
            }
            Console.WriteLine($"\n{grafo.QuantidadeVertices} vértices; " +
                $"{(grafo.Direcionado ? "direcionado" : "não direcionado")}; " +
                $"{(grafo.Ponderado ? "ponderado" : "não ponderado")}; " +
                $"representação {(representacao == "2" ? "matriz" : "lista")}.");
            if (!grafo.Direcionado)
            {
                Console.WriteLine("Fluxo máximo exige grafo direcionado (D=1). Escolha outro arquivo.");
                continue;
            }
            if (grafo.QuantidadeVertices < 2)
            {
                Console.WriteLine("O grafo precisa de pelo menos dois vértices.");
                continue;
            }
            if (!MenuGrafo(grafo)) return 0;
        }
    }

    /// <summary>Modo não interativo: imprime Ford-Fulkerson e busca local direto.</summary>
    private static int ExecutarDireto(string[] args)
    {
        Grafos grafo = new GrafoLista(true, true);
        if (!grafo.carregarDoArquivo(args[0]))
        {
            Console.WriteLine($"Não foi possível carregar '{args[0]}'.");
            return 1;
        }
        int origem = 0, destino = grafo.QuantidadeVertices - 1;
        if (args.Length == 3 && (!int.TryParse(args[1], out origem) || !int.TryParse(args[2], out destino)))
        {
            Console.WriteLine("Origem e destino devem ser números inteiros.");
            return 1;
        }
        try
        {
            Console.WriteLine($"Origem (s) = {origem}, destino (t) = {destino}\n\nFord-Fulkerson:");
            Exibir(FluxoMaximo.FordFulkerson(grafo, origem, destino));
            Console.WriteLine("\nBusca local (inversão de arestas):");
            Exibir(FluxoMaximo.BuscaLocal(grafo, origem, destino));
            return 0;
        }
        catch (ArgumentException erro) { Console.WriteLine(erro.Message); return 1; }
    }

    private static bool MenuGrafo(Grafos grafo)
    {
        int origem = 0, destino = grafo.QuantidadeVertices - 1;
        EscolherOrigemDestino(grafo, ref origem, ref destino);
        while (true)
        {
            Console.WriteLine($"\nOrigem (s) = {origem}, destino (t) = {destino}");
            Console.WriteLine("1) Ford-Fulkerson\n2) Busca local (inversão de arestas)" +
                "\n3) Alterar origem/destino\n4) Escolher outro arquivo\n0) Sair");
            Console.Write("Opção: ");
            string? entrada = Console.ReadLine();
            if (entrada is null or "0") return false;
            try
            {
                switch (entrada)
                {
                    case "1":
                        Console.WriteLine("\nFord-Fulkerson:");
                        Exibir(FluxoMaximo.FordFulkerson(grafo, origem, destino));
                        break;
                    case "2":
                        Console.WriteLine("\nBusca local (inversão de arestas):");
                        Exibir(FluxoMaximo.BuscaLocal(grafo, origem, destino));
                        break;
                    case "3": EscolherOrigemDestino(grafo, ref origem, ref destino); break;
                    case "4": return true;
                    default: Console.WriteLine("Opção inválida."); break;
                }
            }
            catch (ArgumentException erro) { Console.WriteLine(erro.Message); }
        }
    }

    private static void EscolherOrigemDestino(Grafos grafo, ref int origem, ref int destino)
    {
        int ultimo = grafo.QuantidadeVertices - 1;
        origem = LerVertice($"Vértice de origem (0..{ultimo}) [{origem}]: ", origem, ultimo);
        destino = LerVertice($"Vértice de destino (0..{ultimo}) [{destino}]: ", destino, ultimo);
    }

    private static int LerVertice(string pergunta, int padrao, int ultimo)
    {
        while (true)
        {
            Console.Write(pergunta);
            string? texto = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(texto)) return padrao;
            if (int.TryParse(texto, out int v) && v >= 0 && v <= ultimo) return v;
            Console.WriteLine("Vértice inválido.");
        }
    }

    internal static void Exibir(ResultadoFluxo r)
    {
        Console.WriteLine($"Fluxo máximo: {r.FluxoMaximo:G9}");
        Console.WriteLine($"Caminhos aumentantes: {r.CaminhosAumentantes}");
        Console.WriteLine($"Tempo de execução: {r.Duracao.TotalMilliseconds:F6} ms");
        if (r.Arestas.Count > LimiteImpressaoArestas) return;
        Console.WriteLine("Fluxo / capacidade por aresta:");
        foreach (var a in r.Arestas)
            Console.WriteLine($"  {a.Origem} -> {a.Destino}: {a.Fluxo:G9} / {a.Capacidade:G9}");
    }

    internal static void Exibir(ResultadoBuscaLocal r)
    {
        Console.WriteLine($"Fluxo máximo da solução original: {r.FluxoOriginal:G9}");
        Console.WriteLine($"Fluxo máximo da solução final:    {r.FluxoFinal:G9}");
        Console.WriteLine($"Número de passos: {r.Passos}");
        Console.WriteLine($"Soluções avaliadas (execuções do Ford-Fulkerson): {r.SolucoesAvaliadas}");
        Console.WriteLine($"Tempo de execução: {r.Duracao.TotalMilliseconds:F3} ms");
        for (int i = 0; i < r.Inversoes.Count; i++)
        {
            var inv = r.Inversoes[i];
            Console.WriteLine($"  Passo {i + 1}: {inv.Origem} -> {inv.Destino} vira {inv.Destino} -> {inv.Origem} " +
                $"(capacidade {inv.Capacidade:G9}); fluxo = {inv.FluxoApos:G9}");
        }
    }

    private static List<string> LocalizarArquivos()
    {
        var encontrados = new HashSet<string>(StringComparer.Ordinal);
        // O caminho do projeto, derivado do executável, permite rodar com --project da pasta pai.
        string projeto = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var raizes = new[] { Directory.GetCurrentDirectory(), projeto, Path.GetDirectoryName(projeto)! };
        foreach (string raiz in raizes.Distinct())
            foreach (string pasta in new[] { raiz, Path.Combine(raiz, "grafos") })
            {
                if (!Directory.Exists(pasta)) continue;
                try
                {
                    foreach (string arquivo in Directory.EnumerateFiles(pasta, "*.txt"))
                        encontrados.Add(Path.GetFullPath(arquivo));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        return encontrados.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }
}
