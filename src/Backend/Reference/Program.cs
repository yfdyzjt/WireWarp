namespace WireWarp.Backend.Reference;

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("WireWarp backend starting, waiting for frontend pipe...");

        while (true)
        {
            try
            {
                Transport.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connect failed: {e}");
                return;
            }

            Console.WriteLine("Frontend connected.");

            try
            {
                while (true)
                    Transport.ReadRequest();
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("Pipe closed by frontend.");
            }
            catch (IOException e)
            {
                Console.WriteLine($"Pipe error: {e.Message}");
            }
            catch (InvalidDataException e)
            {
                Console.WriteLine($"Protocol error: {e.Message}");
            }

            Transport.Close();
            Console.WriteLine("Waiting for frontend...");
        }
    }
}
