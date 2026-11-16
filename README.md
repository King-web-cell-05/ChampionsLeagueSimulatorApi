# ⚽ FootballSim API

### The Engine Behind Every Competition.

**A scalable football competition management and simulation REST API built with ASP.NET Core 10, C#, Entity Framework Core, and PostgreSQL.**

FootballSim API powers the backend operations of the FootballSim platform, including user authentication, competition management, fixture generation, match simulation, league standings, and administrative functionality.

**Designed & Developed by Dada Kingsley Oluwasanmi**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-Programming-239120?style=for-the-badge&logo=sharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Database-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Supabase](https://img.shields.io/badge/Supabase-Cloud_Database-3FCF8E?style=for-the-badge&logo=supabase&logoColor=white)](https://supabase.com/)
[![Render](https://img.shields.io/badge/Render-Deployment-46E3B7?style=for-the-badge&logo=render&logoColor=black)](https://render.com/)

**[Launch FootballSim](https://king-footballsimulator.vercel.app)** · **[Developer GitHub](https://github.com/King-web-cell-05)**

---

## 📖 Table of Contents

1. Overview
2. Project Objectives
3. Core Features
4. Technology Stack
5. System Architecture
6. Backend Project Structure
7. Database Design
8. Authentication and Authorization
9. Competition Management
10. Fixture Generation Engine
11. Match Simulation Engine
12. League Standings Engine
13. Administrative API
14. REST API Endpoints
15. Installation and Setup
16. Environment Configuration
17. Database Migrations
18. API Testing
19. Deployment
20. Security Considerations
21. Engineering Challenges
22. Future Improvements
23. Developer

---

## 🌍 Overview

**FootballSim API** is the backend service responsible for managing the core operations of FootballSim, a full-stack football competition management and simulation application.

The API provides a structured environment for creating competitions, registering participating teams, generating match fixtures, simulating football matches, and calculating competition standings.

It also manages user authentication, account verification, competition ownership, and administrative operations.

The backend is developed using **ASP.NET Core Web API** and follows a service-oriented architecture that separates HTTP request handling from business logic and database operations.

### Project Goals

- Develop a maintainable football simulation backend.
- Provide reliable RESTful API endpoints.
- Implement secure user authentication and authorization.
- Manage competitions and participating teams.
- Generate football fixtures using dedicated scheduling logic.
- Simulate football matches and persist results.
- Calculate standings from match statistics.
- Support user-specific competition ownership.
- Provide administrative management functionality.
- Integrate with a modern Next.js frontend.
- Deploy the API using cloud infrastructure.

---

## ✨ Core Features

### 🔐 Authentication and Account Management

FootballSim API provides account authentication functionality using JWT.

Implemented authentication functionality includes:

- User registration.
- User login.
- Email verification support.
- Password hashing.
- JWT access token generation.
- Authenticated API access.
- Role-based authorization.
- Account security management.

The backend issues authentication tokens that can be used by authorized clients.

The Next.js frontend manages browser sessions using secure HttpOnly cookies.

### 🏆 Competition Management

The competition module handles the creation and management of football competitions.

Capabilities include:

- Creating competitions.
- Retrieving competitions.
- Associating competitions with their owners.
- Managing participating clubs.
- Preventing duplicate team participation.
- Supporting competition-specific operations.

Competition ownership helps ensure that users interact with their own competition data.

### ⚽ Team Management

The team module manages participating football clubs.

The system includes:

- Team entities.
- Team identifiers.
- Team-related API operations.
- Competition-team relationships.
- Team information used during fixture generation.
- Team information used in league standings.

### 📅 Fixture Generation

FootballSim includes dedicated services for generating football fixtures.

The scheduling system is responsible for pairing participating teams and organizing matches.

Its architecture supports further development of advanced competition formats, including Champions League-style league-phase scheduling.

Fixture generation is separated from match simulation so that scheduling rules can be maintained independently.

### 🎮 Match Simulation

The match simulation engine processes competition fixtures and generates match results.

Its responsibilities include:

- Processing eligible matches.
- Generating match outcomes.
- Updating match records.
- Maintaining simulation results.
- Supplying match statistics to the standings engine.

### 📊 League Standings

The standings engine calculates team performance based on match results.

The system tracks:

| Statistic | Description |
|---|---|
| Played | Total matches played |
| Wins | Matches won |
| Draws | Matches drawn |
| Losses | Matches lost |
| GoalsFor | Total goals scored |
| GoalsAgainst | Total goals conceded |
| GoalDifference | Goals scored minus goals conceded |
| Points | Total competition points |
| Position | Team's ranking |

For standard three-points-for-a-win scoring:

**Points = (Wins × 3) + Draws**

**Goal Difference = Goals For − Goals Against**

### 👑 Administrative Management

FootballSim includes role-protected administrative APIs.

Implemented backend functionality includes:

- Administrative dashboard statistics.
- User listing and pagination.
- User search.
- Account status filtering.
- Individual user details.
- User statistics.
- Administrator profile retrieval and updates.
- Administrator password changes.

Administrative endpoints use authorization rules to restrict access.

---

## 🛠️ Technology Stack

| Technology | Responsibility |
|---|---|
| C# | Backend programming language |
| ASP.NET Core 10 | REST API framework |
| Entity Framework Core | Database access and ORM |
| PostgreSQL | Relational database |
| Supabase | Hosted PostgreSQL infrastructure |
| JWT | Token-based authentication |
| BCrypt | Password hashing |
| Swagger / OpenAPI | API documentation and testing |
| Render | Backend deployment |
| Git & GitHub | Version control |

### Architectural Principles

The backend is organized around:

- Separation of concerns.
- Dependency injection.
- Service-based business logic.
- RESTful API design.
- Entity relationships.
- Data transfer objects.
- Authentication and authorization.
- Environment-based configuration.
- Database migrations.

---

## 🏗️ System Architecture

FootballSim uses a layered backend architecture.

```text
              CLIENT APPLICATION
             (Next.js Frontend)
                      |
                      | HTTPS
                      v
             ASP.NET CORE WEB API
                      |
          +-----------+-----------+
          |           |           |
          v           v           v
      AUTH API   COMPETITION   ADMIN API
                    API
          |           |           |
          +-----------+-----------+
                      |
                      v
              BUSINESS SERVICES
                      |
          +-----------+-----------+
          |           |           |
          v           v           v
      DRAW       SIMULATION    STANDINGS
      SERVICE      SERVICE      SERVICE
          |           |           |
          +-----------+-----------+
                      |
                      v
              ENTITY FRAMEWORK
                      |
                      v
                POSTGRESQL
                 SUPABASE
```

### Architecture Explanation

**Controllers**

Receive HTTP requests, validate request context, and coordinate application operations.

**DTOs**

Define structured request and response data exchanged between clients and the API.

**Services**

Implement business logic, including fixture
