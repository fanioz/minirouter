# Source Tree Integration

## Existing Project Structure
```text
minirouter/
├── Models/              
├── Services/            
├── Tests/               
├── scripts/             
├── docs/                
├── Program.cs           
└── README.md            
```

## New File Organization
```text
minirouter/
├── frontend/                  # New SPA Dashboard
│   ├── src/
│   │   ├── components/        # Shadcn UI components
│   │   ├── App.tsx            # Main application
│   │   └── main.tsx           # Entry point
│   ├── package.json
│   └── vite.config.ts
├── Program.cs                 # Existing file with CORS additions
└── ...
```

## Integration Guidelines
- **File Naming:** standard `camelCase` for utilities, `PascalCase` for React components.
- **Folder Organization:** Colocate components and their related logic.
- **Import/Export Patterns:** Use ES modules.
