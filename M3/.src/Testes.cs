#nullable enable
using TrabalhoM1.Grafos;

namespace TrabalhoM3;

internal static class Testes
{
    private static int verificacoes;

    public static int Executar()
    {
        try
        {
            TestarCasosConhecidos();
            TestarCopiaEPesoZero();
            TestarAleatoriosContraCorteMinimo();
            TestarBuscaLocal();
            TestarArquivos();
            Console.WriteLine($"SUCESSO: {verificacoes} verificações passaram.");
            return 0;
        }
        catch (Exception erro)
        {
            Console.Error.WriteLine($"FALHA após {verificacoes} verificações: {erro}");
            return 1;
        }
    }

    private static void Verificar(bool condicao, string mensagem)
    {
        verificacoes++;
        if (!condicao) throw new InvalidOperationException(mensagem);
    }

    private static bool Perto(double a, double b) => Math.Abs(a - b) <= 1e-4 * Math.Max(1, Math.Abs(b));

    private static Grafos Criar(int n, (int U, int V, float C)[] arestas, bool matriz = false)
    {
        Grafos g = matriz ? new GrafoMatriz(true, true) : new GrafoLista(true, true);
        for (int v = 0; v < n; v++) g.inserirVertice(v.ToString());
        foreach (var a in arestas)
            if (!g.inserirAresta(a.U, a.V, a.C)) throw new Exception("Fixture inválida.");
        return g;
    }

    /// <summary>Capacidade, conservação e valor do fluxo devolvido por aresta.</summary>
    private static void ValidarFluxo(Grafos g, int s, int t, ResultadoFluxo r)
    {
        var saldo = new double[g.QuantidadeVertices];
        foreach (var a in r.Arestas)
        {
            Verificar(a.Fluxo >= 0 && a.Fluxo <= a.Capacidade + 1e-4, $"Capacidade violada em {a.Origem}->{a.Destino}.");
            saldo[a.Origem] -= a.Fluxo;
            saldo[a.Destino] += a.Fluxo;
        }
        for (int v = 0; v < saldo.Length; v++)
            if (v != s && v != t) Verificar(Math.Abs(saldo[v]) < 1e-3, $"Conservação violada no vértice {v}.");
        Verificar(Perto(saldo[t], r.FluxoMaximo), "Fluxo que chega ao destino difere do fluxo máximo.");
    }

    /// <summary>Corte mínimo por enumeração de todos os conjuntos S com s e sem t.</summary>
    private static double CorteMinimo(int n, (int U, int V, float C)[] arestas, int s, int t)
    {
        double melhor = double.MaxValue;
        for (int mascara = 0; mascara < 1 << n; mascara++)
        {
            if ((mascara >> s & 1) == 0 || (mascara >> t & 1) == 1) continue;
            double corte = arestas.Where(a => (mascara >> a.U & 1) == 1 && (mascara >> a.V & 1) == 0).Sum(a => (double)a.C);
            melhor = Math.Min(melhor, corte);
        }
        return melhor;
    }

    private static readonly (int U, int V, float C)[] Classico =
    [
        (0,1,16), (0,2,13), (1,3,12), (2,1,4), (2,4,14), (3,2,9), (3,5,20), (4,3,7), (4,5,4)
    ];

    private static void TestarCasosConhecidos()
    {
        var casos = new (int N, (int, int, float)[] Arestas, int S, int T, double Esperado)[]
        {
            (6, Classico, 0, 5, 23),
            (2, [(0,1,5)], 0, 1, 5),
            (3, [(0,1,5)], 0, 2, 0),                                // destino inalcançável
            (3, [(0,1,3), (1,0,2), (1,2,3)], 0, 2, 3),              // arestas antiparalelas
            (4, [(0,1,1000), (0,2,1000), (1,2,1), (1,3,1000), (2,3,1000)], 0, 3, 2000),
            (4, [(0,1,0.5f), (0,2,0.25f), (1,3,0.3f), (2,3,1)], 0, 3, 0.55),
            (3, [(0,1,4), (1,1,9), (1,2,4)], 0, 2, 4),              // laço
        };
        foreach (var c in casos)
            foreach (bool matriz in new[] { false, true })
            {
                var g = Criar(c.N, c.Arestas, matriz);
                var r = FluxoMaximo.FordFulkerson(g, c.S, c.T);
                Verificar(Perto(r.FluxoMaximo, c.Esperado), $"Esperado {c.Esperado}, obtido {r.FluxoMaximo}.");
                ValidarFluxo(g, c.S, c.T, r);
            }

        var naoDirecionado = new GrafoLista(false, true);
        naoDirecionado.inserirVertice("0"); naoDirecionado.inserirVertice("1");
        naoDirecionado.inserirAresta(0, 1, 3);
        Verificar(Lanca(() => FluxoMaximo.FordFulkerson(naoDirecionado, 0, 1)), "Não direcionado deve ser rejeitado.");
        var g2 = Criar(2, [(0, 1, 1)]);
        Verificar(Lanca(() => FluxoMaximo.FordFulkerson(g2, 0, 0)), "Origem igual ao destino.");
        Verificar(Lanca(() => FluxoMaximo.FordFulkerson(g2, 0, 5)), "Destino fora do intervalo.");
        Verificar(Lanca(() => FluxoMaximo.FordFulkerson(Criar(2, [(0, 1, -1)]), 0, 1)), "Capacidade negativa.");
    }

    private static bool Lanca(Action acao)
    {
        try { acao(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static void TestarCopiaEPesoZero()
    {
        foreach (bool matriz in new[] { false, true })
        {
            // O grafo original não pode ser alterado pelo algoritmo (cópia profunda).
            var g = Criar(6, Classico, matriz);
            var antes = Classico.Select(a => g.pesoAresta(a.U, a.V)).ToArray();
            FluxoMaximo.FordFulkerson(g, 0, 5);
            FluxoMaximo.BuscaLocal(g, 0, 5);
            Verificar(Classico.Select(a => g.pesoAresta(a.U, a.V)).SequenceEqual(antes), "Original alterado.");
            Verificar(Classico.All(a => !g.existeAresta(a.V, a.U) || Classico.Contains((a.V, a.U, g.pesoAresta(a.V, a.U)))),
                "Original ganhou arestas reversas.");

            // Aresta de capacidade 0 é ignorada pela busca.
            var zero = Criar(3, [(0, 1, 0), (1, 2, 5)], matriz);
            Verificar(FluxoMaximo.FordFulkerson(zero, 0, 2).FluxoMaximo == 0, "Aresta de capacidade 0 foi usada.");
            Verificar(zero.buscaProfundidade(0, 2).Count == 0, "DFS percorreu aresta de peso 0.");

            var caminho = Criar(4, [(0, 1, 1), (1, 2, 1), (2, 3, 1)], matriz).buscaProfundidade(0, 3);
            Verificar(caminho.SequenceEqual([0, 1, 2, 3]), "DFS com destino devolveu caminho errado.");
        }

        // Grafo não ponderado: cada aresta tem capacidade 1.
        var np = new GrafoLista(true, false);
        for (int v = 0; v < 4; v++) np.inserirVertice(v.ToString());
        np.inserirAresta(0, 1); np.inserirAresta(0, 2); np.inserirAresta(1, 3); np.inserirAresta(2, 3);
        Verificar(FluxoMaximo.FordFulkerson(np, 0, 3).FluxoMaximo == 2, "Não ponderado deve usar capacidade 1.");
    }

    private static void TestarAleatoriosContraCorteMinimo()
    {
        var rng = new Random(31);
        for (int caso = 0; caso < 300; caso++)
        {
            int n = rng.Next(2, 8);
            var arestas = new List<(int, int, float)>();
            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                    if (u != v && rng.NextDouble() < 0.4) arestas.Add((u, v, rng.Next(0, 10)));
            int s = rng.Next(n), t;
            do t = rng.Next(n); while (t == s);
            var vetor = arestas.ToArray();
            double esperado = CorteMinimo(n, vetor, s, t);
            foreach (bool matriz in new[] { false, true })
            {
                var g = Criar(n, vetor, matriz);
                var r = FluxoMaximo.FordFulkerson(g, s, t);
                Verificar(Perto(r.FluxoMaximo, esperado), $"Caso {caso}: fluxo {r.FluxoMaximo} ≠ corte {esperado}.");
                ValidarFluxo(g, s, t, r);
            }
        }
    }

    private static void TestarBuscaLocal()
    {
        // Mesmo grafo de grafos/busca_local.txt.
        (int, int, float)[] arestas =
            [(0,1,4), (1,5,4), (2,0,3), (2,5,3), (0,3,2), (5,3,2), (4,0,1), (5,4,1)];
        foreach (bool matriz in new[] { false, true })
        {
            var g = Criar(6, arestas, matriz);
            var r = FluxoMaximo.BuscaLocal(g, 0, 5);
            Verificar(Perto(r.FluxoOriginal, 4), "Fluxo original do exemplo.");
            Verificar(Perto(r.FluxoFinal, 9), "Fluxo final do exemplo.");
            Verificar(r.Passos == 2 && r.Inversoes.Count == 2, "Número de passos do exemplo.");
            Verificar(r.Inversoes[0] is { Origem: 2, Destino: 0 } && r.Inversoes[1] is { Origem: 5, Destino: 3 },
                "Ordem das inversões do exemplo.");
            ValidarBuscaLocal(g, 0, 5, r);
        }

        var rng = new Random(7);
        for (int caso = 0; caso < 60; caso++)
        {
            int n = rng.Next(3, 7);
            var lista = new List<(int, int, float)>();
            for (int u = 0; u < n; u++)
                for (int v = u + 1; v < n; v++)
                    if (rng.NextDouble() < 0.5)
                        lista.Add(rng.Next(2) == 0 ? (u, v, rng.Next(1, 8)) : (v, u, rng.Next(1, 8)));
            var g = Criar(n, lista.ToArray());
            ValidarBuscaLocal(g, 0, n - 1, FluxoMaximo.BuscaLocal(g, 0, n - 1));
        }

        var limitado = FluxoMaximo.BuscaLocal(Criar(6, arestas), 0, 5, maxPassos: 1);
        Verificar(limitado.Passos == 1 && Perto(limitado.FluxoFinal, 7), "Limite de passos.");
    }

    /// <summary>Melhora monotônica, solução final coerente e ótimo local de fato.</summary>
    private static void ValidarBuscaLocal(Grafos original, int s, int t, ResultadoBuscaLocal r)
    {
        Verificar(r.FluxoFinal >= r.FluxoOriginal, "Busca local piorou o fluxo.");
        Verificar(Perto(r.FluxoOriginal, FluxoMaximo.FordFulkerson(original, s, t).FluxoMaximo), "Fluxo original.");
        Verificar(Perto(r.FluxoFinal, FluxoMaximo.FordFulkerson(r.SolucaoFinal, s, t).FluxoMaximo), "Fluxo final.");

        // Reaplicar as inversões ao original deve reproduzir a solução final.
        var refeito = FluxoMaximo.Copiar(original);
        double anterior = r.FluxoOriginal;
        foreach (var inv in r.Inversoes)
        {
            Verificar(refeito.existeAresta(inv.Origem, inv.Destino), "Inversão de aresta inexistente.");
            refeito.removerAresta(inv.Origem, inv.Destino);
            refeito.inserirAresta(inv.Destino, inv.Origem, inv.Capacidade);
            Verificar(inv.FluxoApos > anterior, "Passo sem melhoria estrita.");
            anterior = inv.FluxoApos;
        }
        int arestasFinal = 0, arestasOriginal = 0;
        for (int v = 0; v < original.QuantidadeVertices; v++)
        {
            arestasOriginal += original.retornarVizinhos(v).Count;
            foreach (int u in r.SolucaoFinal.retornarVizinhos(v))
            {
                arestasFinal++;
                Verificar(refeito.existeAresta(v, u) && refeito.pesoAresta(v, u) == r.SolucaoFinal.pesoAresta(v, u),
                    "Solução final não corresponde às inversões.");
            }
        }
        Verificar(arestasFinal == arestasOriginal, "A busca local mudou o número de arestas.");

        // Nenhum vizinho da solução final pode ser melhor.
        var final = r.SolucaoFinal;
        for (int u = 0; u < final.QuantidadeVertices; u++)
            foreach (int v in final.retornarVizinhos(u))
            {
                if (u == v || final.existeAresta(v, u)) continue;
                var vizinha = FluxoMaximo.Copiar(final);
                float c = vizinha.pesoAresta(u, v);
                vizinha.removerAresta(u, v);
                vizinha.inserirAresta(v, u, c);
                Verificar(FluxoMaximo.FordFulkerson(vizinha, s, t).FluxoMaximo <= r.FluxoFinal + 1e-4,
                    "Solução final não é ótimo local.");
            }
    }

    private static void TestarArquivos()
    {
        string pasta = Path.Combine(AppContext.BaseDirectory, "grafos");
        var esperados = new (string Arquivo, double Fluxo, double Final)[]
        {
            ("classico.txt", 23, 24), ("busca_local.txt", 4, 9), ("antiparalelas.txt", 3, 3)
        };
        foreach (var e in esperados)
        {
            Grafos g = new GrafoLista(true, true);
            Verificar(g.carregarDoArquivo(Path.Combine(pasta, e.Arquivo)), $"Leitura de {e.Arquivo}.");
            int t = g.QuantidadeVertices - 1;
            Verificar(Perto(FluxoMaximo.FordFulkerson(g, 0, t).FluxoMaximo, e.Fluxo), $"Fluxo de {e.Arquivo}.");
            Verificar(Perto(FluxoMaximo.BuscaLocal(g, 0, t).FluxoFinal, e.Final), $"Busca local de {e.Arquivo}.");
        }
    }
}
