using System;
using System.Collections.Generic;

namespace Biblioteca
{
    public class Livro
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Autor { get; set; }
        public string Genero { get; set; }
        public bool Disponivel { get; set; }
        public DateTime? DataEmprestimo { get; set; }
        public string EmailUsuario { get; set; }
        public string NomeUsuario { get; set; }
    }

    public interface ICalculadoraMulta
    {
        decimal Calcular(Livro livro);
    }

    public class CalculadoraMultaPadrao : ICalculadoraMulta
    {
        private readonly decimal _valorPorDia;
        private readonly int _diasLimite;

        public CalculadoraMultaPadrao(decimal valorPorDia = 2.50m, int diasLimite = 14)
        {
            _valorPorDia = valorPorDia;
            _diasLimite = diasLimite;
        }

        public decimal Calcular(Livro livro)
        {
            if (livro.DataEmprestimo == null) return 0;
            var diasAtraso = (DateTime.Now - livro.DataEmprestimo.Value).Days - _diasLimite;
            return diasAtraso > 0 ? diasAtraso * _valorPorDia : 0;
        }
    }

    public interface IEmailService
    {
        void Enviar(string para, string assunto, string corpo);
    }

    public class EmailServiceConsole : IEmailService
    {
        public void Enviar(string para, string assunto, string corpo)
        {
            Console.WriteLine($"[EMAIL] Para: {para} | Assunto: {assunto} | Corpo: {corpo}");
        }
    }

    public interface IRepository<T>
    {
        void Salvar(T entity);
        T Buscar(int id);
        List<T> BuscarTodos();
    }

    public class LivroRepository : IRepository<Livro>
    {
        public void Salvar(Livro livro)
        {
            Console.WriteLine($"[DB] INSERT INTO livros VALUES ({livro.Id}, '{livro.Titulo}', '{livro.Autor}', {livro.Disponivel})");
        }

        public Livro Buscar(int id)
        {
            Console.WriteLine($"[DB] SELECT * FROM livros WHERE id = {id}");
            return new Livro(); 
        }

        public List<Livro> BuscarTodos()
        {
            Console.WriteLine("[DB] SELECT * FROM livros");
            return new List<Livro>(); 
        }
    }

    public interface IGeradorRelatorio
    {
        string Gerar(Livro livro);
    }

    public class GeradorRelatorioSimples : IGeradorRelatorio
    {
        private readonly ICalculadoraMulta _calculadoraMulta;

        public GeradorRelatorioSimples(ICalculadoraMulta calculadoraMulta)
        {
            _calculadoraMulta = calculadoraMulta;
        }

        public string Gerar(Livro livro)
        {
            var multa = _calculadoraMulta.Calcular(livro);
            return $"Livro: {livro.Titulo} | Autor: {livro.Autor} | Disponível: {livro.Disponivel} | Multa: R${multa}";
        }
    }

    public interface IDescontoStrategy
    {
        decimal CalcularDesconto(decimal valorMulta);
        string TipoUsuario { get; }
    }

    public class DescontoEstudante : IDescontoStrategy
    {
        public string TipoUsuario => "Estudante";
        public decimal CalcularDesconto(decimal valorMulta) => valorMulta * 0.50m;
    }

    public class DescontoProfessor : IDescontoStrategy
    {
        public string TipoUsuario => "Professor";
        public decimal CalcularDesconto(decimal valorMulta) => valorMulta * 0.80m;
    }

    public class DescontoFuncionario : IDescontoStrategy
    {
        public string TipoUsuario => "Funcionario";
        public decimal CalcularDesconto(decimal valorMulta) => valorMulta * 0.30m;
    }

    public class SemDesconto : IDescontoStrategy
    {
        public string TipoUsuario => "Padrao";
        public decimal CalcularDesconto(decimal valorMulta) => 0;
    }

    public class DescontoStrategyFactory
    {
        private readonly Dictionary<string, IDescontoStrategy> _estrategias;

        public DescontoStrategyFactory()
        {
            _estrategias = new Dictionary<string, IDescontoStrategy>
            {
                ["Estudante"] = new DescontoEstudante(),
                ["Professor"] = new DescontoProfessor(),
                ["Funcionario"] = new DescontoFuncionario()
            };
        }

        public IDescontoStrategy ObterEstrategia(string tipoUsuario)
        {
            return _estrategias.TryGetValue(tipoUsuario, out var estrategia) 
                ? estrategia 
                : new SemDesconto();
        }
    }

    public class ServicoEmprestimo
    {
        private readonly IRepository<Livro> _livroRepository;
        private readonly ICalculadoraMulta _calculadoraMulta;
        private readonly IEmailService _emailService;
        private readonly DescontoStrategyFactory _descontoFactory;

        public ServicoEmprestimo(
            IRepository<Livro> livroRepository,
            ICalculadoraMulta calculadoraMulta,
            IEmailService emailService,
            DescontoStrategyFactory descontoFactory)
        {
            _livroRepository = livroRepository;
            _calculadoraMulta = calculadoraMulta;
            _emailService = emailService;
            _descontoFactory = descontoFactory;
        }

        public decimal CalcularDesconto(string tipoUsuario, decimal valorMulta)
        {
            var estrategia = _descontoFactory.ObterEstrategia(tipoUsuario);
            return estrategia.CalcularDesconto(valorMulta);
        }

        public void RealizarEmprestimo(Livro livro, string nomeUsuario, string emailUsuario)
        {
            if (!livro.Disponivel)
            {
                Console.WriteLine("Livro indisponível.");
                return;
            }

            livro.Disponivel = false;
            livro.DataEmprestimo = DateTime.Now;
            livro.NomeUsuario = nomeUsuario;
            livro.EmailUsuario = emailUsuario;
            _livroRepository.Salvar(livro);

            Console.WriteLine($"Empréstimo realizado: {livro.Titulo} para {nomeUsuario}");
        }

        public void DevolverLivro(Livro livro, string tipoUsuario)
        {
            var multa = _calculadoraMulta.Calcular(livro);
            var desconto = CalcularDesconto(tipoUsuario, multa);
            var multaFinal = multa - desconto;

            livro.Disponivel = true;
            livro.DataEmprestimo = null;
            _livroRepository.Salvar(livro);

            if (multaFinal > 0)
            {
                Console.WriteLine($"Devolução com multa de R${multaFinal}");
                _emailService.Enviar(livro.EmailUsuario, "Atraso na devolução",
                    $"Você tem uma multa de R${multaFinal} pelo livro '{livro.Titulo}'.");
            }
            else
            {
                Console.WriteLine("Devolução sem multa. Obrigado!");
            }
        }
    }

    public abstract class ItemAcervo
    {
        public string Titulo { get; set; }
        public bool Disponivel { get; set; }

        public abstract void Emprestar(string usuario);
        public abstract void Devolver();
    }

    public interface IReservavel
    {
        void Reservar(string usuario);
    }

    public class LivroFisico : ItemAcervo, IReservavel
    {
        public override void Emprestar(string usuario)
        {
            Disponivel = false;
            Console.WriteLine($"[FÍSICO] '{Titulo}' emprestado para {usuario}.");
        }

        public override void Devolver()
        {
            Disponivel = true;
            Console.WriteLine($"[FÍSICO] '{Titulo}' devolvido.");
        }

        public void Reservar(string usuario)
        {
            Console.WriteLine($"[FÍSICO] '{Titulo}' reservado para {usuario} por 3 dias.");
        }
    }

    public class EbookEmprestavel : ItemAcervo
    {
        public override void Emprestar(string usuario)
        {
            Disponivel = false;
            Console.WriteLine($"[EBOOK] Link de download enviado para {usuario}.");
        }

        public override void Devolver()
        {
            Disponivel = true;
            Console.WriteLine($"[EBOOK] Acesso revogado.");
        }
    }

    public interface IGeradorPDF
    {
        void GerarPDF();
    }

    public interface IGeradorExcel
    {
        void GerarExcel();
    }

    public interface IGeradorHTML
    {
        void GerarHTML();
    }

    public interface IEnviavelPorEmail
    {
        void EnviarPorEmail(string destinatario);
    }

    public interface ISalvavelEmDisco
    {
        void SalvarEmDisco(string caminho);
    }

    public class RelatorioEmprestimos : IGeradorPDF, IEnviavelPorEmail, ISalvavelEmDisco
    {
        public void GerarPDF()
        {
            Console.WriteLine("Gerando PDF de empréstimos...");
        }

        public void EnviarPorEmail(string destinatario)
        {
            Console.WriteLine($"Enviando relatório de empréstimos para {destinatario}");
        }

        public void SalvarEmDisco(string caminho)
        {
            Console.WriteLine($"Salvando relatório em {caminho}");
        }
    }

    public class RelatorioInventario : IGeradorExcel, ISalvavelEmDisco
    {
        public void GerarExcel()
        {
            Console.WriteLine("Gerando Excel de inventário...");
        }

        public void SalvarEmDisco(string caminho)
        {
            Console.WriteLine($"Salvando inventário em {caminho}");
        }
    }

    public interface IBancoDados
    {
        void Salvar(string tabela, string dados);
        List<string> Buscar(string tabela, string filtro);
    }

    public class BancoDadosMySQL : IBancoDados
    {
        public void Salvar(string tabela, string dados)
        {
            Console.WriteLine($"[MySQL] INSERT INTO {tabela}: {dados}");
        }

        public List<string> Buscar(string tabela, string filtro)
        {
            Console.WriteLine($"[MySQL] SELECT * FROM {tabela} WHERE {filtro}");
            return new List<string> { "resultado simulado" };
        }
    }

    public class BancoDadosPostgreSQL : IBancoDados
    {
        public void Salvar(string tabela, string dados)
        {
            Console.WriteLine($"[PostgreSQL] INSERT INTO {tabela}: {dados}");
        }

        public List<string> Buscar(string tabela, string filtro)
        {
            Console.WriteLine($"[PostgreSQL] SELECT * FROM {tabela} WHERE {filtro}");
            return new List<string> { "resultado simulado" };
        }
    }

    public class ServicoEmailSendGrid : IEmailService
    {
        public void Enviar(string para, string assunto, string corpo)
        {
            Console.WriteLine($"[SendGrid] Para: {para} | Assunto: {assunto} | Corpo: {corpo}");
        }
    }

    public class GerenciadorAcervo
    {
        private readonly IBancoDados _banco;
        private readonly IEmailService _email;
        private readonly IRepository<Livro> _livroRepository;

        public GerenciadorAcervo(IBancoDados banco, IEmailService email, IRepository<Livro> livroRepository)
        {
            _banco = banco;
            _email = email;
            _livroRepository = livroRepository;
        }

        public void CadastrarLivro(Livro livro)
        {
            _banco.Salvar("livros", $"'{livro.Titulo}', '{livro.Autor}'");
            _livroRepository.Salvar(livro);
            Console.WriteLine($"Livro '{livro.Titulo}' cadastrado.");
        }

        public void NotificarAtraso(string emailUsuario, string tituloLivro, decimal multa)
        {
            _email.Enviar(emailUsuario, "Atraso na devolução",
                $"Você tem uma multa de R${multa} pelo livro '{tituloLivro}'.");
        }

        public List<string> BuscarLivrosDisponiveis()
        {
            return _banco.Buscar("livros", "disponivel = true");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Sistema de Biblioteca (Versão Refatorada) ===\n");

            var livroRepository = new LivroRepository();
            var calculadoraMulta = new CalculadoraMultaPadrao();
            var emailService = new EmailServiceConsole();
            var descontoFactory = new DescontoStrategyFactory();
            
            var servicoEmprestimo = new ServicoEmprestimo(
                livroRepository, calculadoraMulta, emailService, descontoFactory);

            var livro = new Livro
            {
                Id = 1,
                Titulo = "Clean Code",
                Autor = "Robert C. Martin",
                Genero = "Tecnologia",
                Disponivel = true,
                EmailUsuario = "aluno@faculdade.edu",
                NomeUsuario = "João Silva"
            };

            servicoEmprestimo.RealizarEmprestimo(livro, "João Silva", "aluno@faculdade.edu");

            livro.DataEmprestimo = DateTime.Now.AddDays(-20);
            servicoEmprestimo.DevolverLivro(livro, "Estudante");

            Console.WriteLine("\n--- Polimorfismo (LSP corrigido) ---");
            var itens = new List<ItemAcervo>
            {
                new LivroFisico { Titulo = "Design Patterns", Disponivel = true },
                new EbookEmprestavel { Titulo = "Refactoring", Disponivel = true }
            };

            foreach (var item in itens)
            {
                item.Emprestar("Maria Souza");
                
                if (item is IReservavel reservavel)
                {
                    reservavel.Reservar("Carlos");
                }
            }

            Console.WriteLine("\n--- Relatórios (ISP corrigido) ---");
            var relEmprestimos = new RelatorioEmprestimos();
            relEmprestimos.GerarPDF();
            relEmprestimos.EnviarPorEmail("admin@biblioteca.com");

            var relInventario = new RelatorioInventario();
            relInventario.GerarExcel();

            Console.WriteLine("\n--- Gerenciador (DIP corrigido) ---");
            var bancoDados = new BancoDadosMySQL();
            var gerenciador = new GerenciadorAcervo(bancoDados, emailService, livroRepository);
            gerenciador.CadastrarLivro(livro);
            gerenciador.NotificarAtraso(livro.EmailUsuario, livro.Titulo, 15.00m);

            Console.WriteLine("\n=== Sistema refatorado com sucesso! ===");
        }
    }
}