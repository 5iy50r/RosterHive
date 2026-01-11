# RosterHive

RosterHive to aplikacja webowa w ASP.NET Core MVC (.NET 8) do zarządzania zespołami, grafikiem zmian, nieobecnościami, podmianami zmian oraz zadaniami. Zawiera moduły raportowe (agregacje i grupowania w bazie) oraz panel administracyjny.

## Wymagania
- Visual Studio 2022
- .NET 8 SDK

## Pierwsze uruchomienie
1. Sklonuj repozytorium lub pobierz projekt i otwórz plik `RosterHive.sln` w Visual Studio.
2. Upewnij się, że projekt `RosterHive` jest ustawiony jako startowy.
3. Otwórz: **Tools → NuGet Package Manager → Package Manager Console**.
4. W konsoli wykonaj polecenie: Update-Database
5. Uruchom aplikację.

Od tego momentu, po zalogowaniu na konto root (login: admin@rosterhive.local, password: Admin123!), użytkownik może rozpocząć wykonywanie operacji wewnątrz gotowej aplikacji. Obejmuje to operacje na użytkownikach, zespołach, zadaniach oraz innych uwzględnionych modułach.
