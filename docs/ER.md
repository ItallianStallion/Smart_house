# Схема бази даних «Розумний будинок» (PostgreSQL)

```mermaid
erDiagram
    Homes ||--o{ Rooms : "містить"
    Rooms ||--o{ Devices : "має"
    Rooms ||--o{ Telemetry : "історія вимірювань"
    Homes { text Id PK  text Name  text Address }
    Rooms { text Id PK  text Name  text Type  integer Floor  integer TargetTemperature  text CurrentStatus  text LastSync  text HomeId FK }
    Devices { text Id PK  text Name  text Type  integer IsEnabled  text LastUpdated  text RoomId FK }
    Telemetry { text RoomId PK_FK  integer Id PK  double Temperature  double Humidity  double EnergyConsumption  text Timestamp }
```
