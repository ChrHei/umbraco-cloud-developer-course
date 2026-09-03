```mermaid
flowchart TD
    A[Push till main<br/>eller manuell körning] --> B[main.yml]

    B --> C[cloud-sync.yml<br/>Umbraco Cloud Sync]

    subgraph Sync[Synkronisera ändringar från Umbraco Cloud]
        C --> D[preflight]
        D --> E[Get-LatestDeployment.ps1]
        E --> F{Finns senaste<br/>CICD-deploy?}

        F -- Nej --> G[Ingen remote diff]
        F -- Ja --> H[checkForChanges]
        H --> I[Get-ChangesById.ps1]
        I --> J{Ändringar från Cloud?}

        J -- Nej --> G
        J -- Ja --> K[Ladda ner git-patch.diff]
        K --> L[Spara patch som pipeline-artifact]
        L --> M[applyRemoteChanges]
        M --> N[Apply-Patch.ps1]
        N --> O{Patch redan applicerad?}
        O -- Ja --> P[Hoppa över applicering]
        O -- Nej --> Q[Testa om patch kan appliceras]
        Q --> R{Kan patch appliceras?}
        R -- Nej --> S[Workflow misslyckas]
        R -- Ja --> T[Applicera patch]
        T --> U[Commit + push till aktuell branch]
        U --> V[Returnera updatedSha]
    end

    G --> W[cloud-artifact.yml]
    P --> W
    V --> W

    subgraph Artifact[Skapa deploy-artifact]
        W --> X[Checkout newSha<br/>eller standardbranch]
        X --> Y[Byt .gitignore mot cloud.gitignore]
        Y --> Z[Zippar källkod enligt cloud.zipignore]
        Z --> AA[Spara sources.zip<br/>som pipeline-artifact]
        AA --> AB[Add-DeploymentArtifact.ps1]
        AB --> AC[POST zip till Umbraco Cloud API]
        AC --> AD[Returnera artifactId]
    end

    AD --> AE[cloud-deployment.yml]

    subgraph Deploy[Deploya till Umbraco Cloud]
        AE --> AF[startDeployment]
        AF --> AG[Start-Deployment.ps1]
        AG --> AH[POST deployment request]
        AH --> AI[Returnera deploymentId]
        AI --> AJ[awaitDeploymentFinished]
        AJ --> AK[Test-DeploymentStatus.ps1]
        AK --> AL{Status}
        AL -- Pending / Queued / InProgress --> AM[Vänta 25 sekunder]
        AM --> AK
        AL -- Completed --> AN[Deploy lyckades]
        AL -- Failed --> AO[Deploy misslyckades]
        AL -- Timeout / oväntad status --> AO
    end

    style A fill:#4dac22
    style AN fill:#4dac22
    style S fill:#c1211a
    style AO fill:#c1211a
```

Centrala dataövergångar:

- latestDeploymentId hämtas från Cloud.
- remoteChanges avgör om en patch ska laddas ner och appliceras.
- updatedSha används för artifact-checkout efter synkronisering.
- artifactId används för att starta Cloud-deployen.
- deploymentId används för polling tills deployen är klar.

Manuell deploy kan även startas direkt via cloud-deployment.yml med ett befintligt artifactId.