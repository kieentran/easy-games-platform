# Easy Games Software

Easy Games Software is a full-stack e-commerce platform for selling **books, games, and toys** both online and through **physical shop locations**.

The system supports multiple user roles and includes features such as **inventory management, POS sales, tier-based customer rewards, and email marketing campaigns**.

This project was developed for **HIT339 Distributed Development** at Charles Darwin University.

---

## Features

### Admin / Owner
- Full system management
- Stock CRUD operations
- User and role management
- Shop location management
- Financial reports and analytics
- Tier-based customer rewards system
- Email marketing campaigns

### Shop Proprietor
- Shop dashboard and statistics
- Shop inventory management
- Stock transfer from main inventory
- Point of Sale (POS) system
- Customer lookup and quick registration
- Tier-based discount application
- Receipt generation

### Customer
- Product browsing by category
- Shopping cart and checkout
- Purchase history tracking
- Reward tier status and points
- Account management

---

## Technologies Used

- **ASP.NET Core MVC**
- **C#**
- **.NET 7**
- **SQLite**
- **Entity Framework Core**
- **Bootstrap**
- **JavaScript**

---

## System Requirements

- Windows 10 / 11 or macOS 10.15+
- **Visual Studio 2022**
- **.NET 7 SDK**
- Git (optional)

---

## Running the Project

1. Clone the repository
2. Open the solution file:


Easy Games Software.sln


3. Restore NuGet packages if required
4. Build the solution
5. Run the project with **F5**

The SQLite database (`easygames.db`) will be created automatically on first run.

---

## Default Accounts

Admin 

- Username: admin
- Password: admin123

Sample Customer 

- Username: customer1
- Password: password123


Sample Shop Proprietor  

- Username: shop1
- Password: shop123

---

## Key System Concepts

### Customer Reward Tiers
| Tier | Points | Discount |
|-----|------|------|
| Bronze | 0-49 | 0% |
| Silver | 50-99 | 5% |
| Gold | 100-199 | 10% |
| Platinum | 200+ | 15% |

Customers earn **1 point for every $10 spent**.

---

## Project Team

- **Vinh Kien Tran** – Project Lead & POS System Development  
- Keagen Leon Smith – Tier Management & QA  
- Quan Sinh Phat Nguyen – Email Marketing System

---

## Project Highlights

- Multi-shop inventory management system
- Point-of-Sale (POS) system for physical stores
- Tier-based reward and discount system
- Email marketing campaigns
- Role-based access control
- Sales and financial analytics

---
