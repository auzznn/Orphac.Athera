export type SupportedLocale = "id-ID" | "en-US";

export interface Dictionary {
  appName: string;
  envLabel: string;
  localeLabel: string;
  login: {
    heading: string;
    subtitle: string;
    emailLabel: string;
    emailPlaceholder: string;
    passwordLabel: string;
    rememberLabel: string;
    submitIdle: string;
    submitBusy: string;
    showPassword: string;
    hidePassword: string;
    securityNoteTitle: string;
    securityPoints: string[];
    demoHintTitle: string;
    demoHintBody: string;
    errors: Record<string, string>;
  };
  shell: {
    skipToMain: string;
    scopeUnboundBadge: string;
    scopeCompactBadge: string;
    scopeUnboundHint: string;
    scopeBoundHint: string;
    selectScope: string;
    switchScopeLabel: string;
    scopeMenuHeading: string;
    openDirectoryLink: string;
    activeTag: string;
    signOut: string;
    signingOut: string;
    logoutError: string;
    navTitle: string;
    openMenu: string;
    closeMenu: string;
    navItems: {
      overview: string;
      workspaces: string;
      procurement: string;
      inventory: string;
      sales: string;
      finance: string;
      admin: string;
    };
    lockedSuffix: string;
    permittedSuffix: string;
    modulePlannedHint: string;
    sessionHeading: string;
    sessionLead: string;
    recordCardTitle: string;
    fieldUser: string;
    fieldEmail: string;
    fieldLocale: string;
    fieldLastLogin: string;
    fieldWorkspaceStatus: string;
    fieldAuthorizedCount: string;
    fieldDbVerification: string;
    dbVerifiedValue: string;
    boundaryTitle: string;
    boundaryBody: string;
    nextStepHeading: string;
    nextStepBodyActive: string;
    nextStepBodyEmpty: string;
    openDirectoryCta: string;
    loadingSession: string;
    errorHeading: string;
    errorRetry: string;
    errorBackToLogin: string;
  };
  workspaces: {
    heading: string;
    lead: string;
    tenantCountLabel: string;
    entityCountLabel: string;
    colCode: string;
    colName: string;
    colEntity: string;
    colNpwp: string;
    colFiscal: string;
    colFiscalProfile: string;
    colGrants: string;
    colAction: string;
    enterWorkspace: string;
    activeStatus: string;
    noNpwp: string;
    emptyTitle: string;
    emptyBody: string;
    emptyIdentityRef: string;
    emptyGuidanceTitle: string;
    emptyGuidanceStep1: string;
    emptyGuidanceStep2: string;
  };
  entitySheet: {
    backToDirectory: string;
    contextCardTitle: string;
    fieldTenant: string;
    fieldEntityCode: string;
    fieldEntityName: string;
    fieldNpwp: string;
    fieldCurrency: string;
    fieldCountryTz: string;
    fieldEvaluatedAt: string;
    permissionsCardTitle: string;
    colModule: string;
    colPermission: string;
    colReadiness: string;
    availableState: string;
    permittedPlannedPrefix: string;
    notGrantedState: string;
    loadingEntity: string;
    unavailableTitle: string;
    unavailableBody: string;
    forbiddenTitle: string;
    forbiddenBody: string;
    returnWorkspaces: string;
    moduleNames: Record<string, string>;
  };
}

export const MESSAGES: Record<SupportedLocale, Dictionary> = {
  "id-ID": {
    appName: "Orphac Athera",
    envLabel: "Fondasi Terverifikasi · PostgreSQL",
    localeLabel: "Bahasa",
    login: {
      heading: "Masuk ke Sistem Operasional",
      subtitle: "Gunakan akun ASP.NET Core Identity yang terdaftar pada direktori organisasi Anda.",
      emailLabel: "Email Perusahaan",
      emailPlaceholder: "nama@perusahaan.co.id",
      passwordLabel: "Kata Sandi",
      rememberLabel: "Pertahankan sesi pada peramban ini (8 jam bergulir)",
      submitIdle: "Masuk ke Athera",
      submitBusy: "Memverifikasi kredensial...",
      showPassword: "Tampilkan kata sandi",
      hidePassword: "Sembunyikan kata sandi",
      securityNoteTitle: "Batas Keamanan Sesi",
      securityPoints: [
        "Kredensial hanya diverifikasi oleh server ASP.NET Core Identity.",
        "Cookie sesi HttpOnly dengan perlindungan CSRF pada setiap aksi mutasi.",
        "Akses data tenant dan entitas hukum diisolasi per pengguna oleh PostgreSQL RLS.",
      ],
      demoHintTitle: "Lingkungan Uji Lokal (Data Sintetis)",
      demoHintBody: "Untuk verifikasi pengembangan lokal gunakan akun demo ter-seed (@demo.athera.local).",
      errors: {
        invalid_credentials: "Email atau kata sandi tidak sesuai. Periksa kembali kredensial Anda.",
        account_locked: "Akun dikunci sementara setelah beberapa kali gagal masuk. Tunggu beberapa menit.",
        account_inactive: "Akun Anda dinonaktifkan. Hubungi Administrator Organisasi.",
        csrf_validation_failed: "Token keamanan formulir kedaluwarsa. Muat ulang dan coba lagi.",
        validation_error: "Isi alamat email dan kata sandi terlebih dahulu.",
        upstream_unavailable: "Layanan API backend tidak dapat dijangkau. Pastikan API berjalan.",
        unknown: "Gagal memproses permintaan masuk. Coba beberapa saat lagi.",
      },
    },
    shell: {
      skipToMain: "Lewati ke konten utama",
      scopeUnboundBadge: "Tanpa Ruang Kerja Aktif (Default-Deny)",
      scopeCompactBadge: "Belum ada ruang kerja aktif",
      scopeUnboundHint: "Hubungi Administrator Organisasi untuk keanggotaan tenant dan izin entitas.",
      scopeBoundHint: "Konteks tenant dan entitas dikunci server melalui PostgreSQL RLS per transaksi.",
      selectScope: "Pilih Entitas",
      switchScopeLabel: "Ganti ruang kerja entitas",
      scopeMenuHeading: "Entitas Hukum Diotorisasi",
      openDirectoryLink: "Buka Direktori Ruang Kerja",
      activeTag: "Aktif",
      signOut: "Keluar",
      signingOut: "Mengakhiri sesi...",
      logoutError: "Gagal mengakhiri sesi dengan aman. Coba lagi.",
      navTitle: "Modul Operasional",
      openMenu: "Buka menu navigasi",
      closeMenu: "Tutup menu navigasi",
      navItems: {
        overview: "Ringkasan Sesi",
        workspaces: "Ruang Kerja Entitas",
        procurement: "Pengadaan",
        inventory: "Persediaan",
        sales: "Penjualan",
        finance: "Keuangan & GL",
        admin: "Administrasi",
      },
      lockedSuffix: "Tidak diotorisasi pada entitas ini",
      permittedSuffix: "Izin aktif; modul dibuka pada slice berikutnya",
      modulePlannedHint: "Segera",
      sessionHeading: "Ringkasan Identitas & Otorisasi Sesi",
      sessionLead: "Status akun dan ruang kerja aktif dimuat langsung dari PostgreSQL dengan isolasi peran minimum.",
      recordCardTitle: "Rekam Identitas PostgreSQL",
      fieldUser: "Nama Tampilan",
      fieldEmail: "Email Terdaftar",
      fieldLocale: "Preferensi Bahasa",
      fieldLastLogin: "Masuk Terakhir (WIB)",
      fieldWorkspaceStatus: "Status Ruang Kerja",
      fieldAuthorizedCount: "Entitas Diotorisasi",
      fieldDbVerification: "Koneksi & Peran Basis Data",
      dbVerifiedValue: "Terhubung ke PostgreSQL (peran runtime non-superuser + RLS aktif)",
      boundaryTitle: "Batas Keamanan Multi-Tenant Aktif",
      boundaryBody:
        "Setiap permintaan ruang kerja memvalidasi keanggotaan dan izin entitas di server sebelum mengunci app.tenant_id lokal transaksi.",
      nextStepHeading: "Ruang Kerja Entitas Hukum",
      nextStepBodyActive:
        "Buka direktori ruang kerja untuk masuk ke entitas hukum yang Anda miliki atau berpindah konteks secara aman.",
      nextStepBodyEmpty:
        "Akun ini belum memiliki keanggotaan tenant atau izin entitas aktif. Buka direktori ruang kerja untuk panduan aktivasi akses.",
      openDirectoryCta: "Buka Direktori Ruang Kerja",
      loadingSession: "Memeriksa sesi aktif...",
      errorHeading: "Gagal Memverifikasi Sesi",
      errorRetry: "Coba Lagi",
      errorBackToLogin: "Kembali ke Masuk",
    },
    workspaces: {
      heading: "Direktori Ruang Kerja",
      lead: "Hanya tenant dan entitas hukum dengan keanggotaan serta izin aktif yang ditampilkan untuk akun Anda.",
      tenantCountLabel: "Tenant aktif",
      entityCountLabel: "Entitas diotorisasi",
      colCode: "Kode Entitas",
      colName: "Nama Entitas Hukum",
      colEntity: "Entitas Hukum",
      colNpwp: "NPWP",
      colFiscal: "Mata Uang & Zona Waktu",
      colFiscalProfile: "NPWP & Konteks Fiskal",
      colGrants: "Izin Aktif",
      colAction: "Tindakan",
      enterWorkspace: "Masuk Ruang Kerja",
      activeStatus: "Aktif",
      noNpwp: "Belum diisi",
      emptyTitle: "Belum Ada Ruang Kerja yang Diotorisasi",
      emptyBody:
        "Identitas ASP.NET Core Identity Anda terverifikasi, namun belum memiliki keanggotaan tenant maupun izin entitas hukum yang aktif. Akses bersifat ditolak secara default.",
      emptyIdentityRef: "Referensi Akun Terautentikasi",
      emptyGuidanceTitle: "Cara Memperoleh Akses Ruang Kerja",
      emptyGuidanceStep1: "Minta Administrator Organisasi menambahkan keanggotaan tenant dan izin entitas untuk email akun Anda.",
      emptyGuidanceStep2: "Alur undangan mandiri & persetujuan empat mata tersedia pada tahap berikutnya (S02).",
    },
    entitySheet: {
      backToDirectory: "Direktori Ruang Kerja",
      contextCardTitle: "Profil Entitas Hukum & Konteks Fiskal",
      fieldTenant: "Tenant Induk",
      fieldEntityCode: "Kode Entitas",
      fieldEntityName: "Nama Entitas Hukum",
      fieldNpwp: "NPWP Entitas",
      fieldCurrency: "Mata Uang Fungsional",
      fieldCountryTz: "Yurisdiksi & Zona Waktu",
      fieldEvaluatedAt: "Verifikasi Server (WIB)",
      permissionsCardTitle: "Izin Efektif & Kesiapan Modul",
      colModule: "Modul Operasional",
      colPermission: "Kode Izin",
      colReadiness: "Status Kesiapan",
      availableState: "Tersedia (S01)",
      permittedPlannedPrefix: "Diizinkan · Tahap",
      notGrantedState: "Tidak Diizinkan",
      loadingEntity: "Memuat ruang kerja entitas terisolasi...",
      unavailableTitle: "Ruang Kerja Tidak Tersedia",
      unavailableBody:
        "Tenant atau entitas hukum yang diminta tidak ditemukan atau berada di luar otorisasi aktif akun Anda.",
      forbiddenTitle: "Izin Entitas Tidak Mencukupi",
      forbiddenBody:
        "Anda diotorisasi pada entitas ini, namun izin khusus yang diminta tidak tersedia pada penugasan Anda.",
      returnWorkspaces: "Kembali ke Direktori Ruang Kerja",
      moduleNames: {
        overview: "Ringkasan Entitas & Tata Kelola",
        procurement: "Pengadaan (PO, GRN, Pemasok)",
        inventory: "Persediaan & Buku Besar Stok",
        sales: "Penjualan & Penagihan",
        finance: "Keuangan, Pajak PPN/PPh & GL",
        admin: "Administrasi Organisasi & Izin",
      },
    },
  },
  "en-US": {
    appName: "Orphac Athera",
    envLabel: "Verified Foundation · PostgreSQL",
    localeLabel: "Language",
    login: {
      heading: "Sign In to Operations",
      subtitle: "Authenticate using your organization's ASP.NET Core Identity account.",
      emailLabel: "Work Email",
      emailPlaceholder: "name@company.co.id",
      passwordLabel: "Password",
      rememberLabel: "Keep session active on this browser (8h sliding)",
      submitIdle: "Sign in to Athera",
      submitBusy: "Verifying credentials...",
      showPassword: "Show password",
      hidePassword: "Hide password",
      securityNoteTitle: "Session Security Boundary",
      securityPoints: [
        "Credentials are verified exclusively by ASP.NET Core Identity on the API server.",
        "HttpOnly session cookies paired with CSRF validation on all state-changing requests.",
        "Tenant and legal-entity data are isolated per user by PostgreSQL Row-Level Security.",
      ],
      demoHintTitle: "Local Verification Environment (Synthetic Fixture)",
      demoHintBody: "For local development verification, sign in with a seeded @demo.athera.local identity.",
      errors: {
        invalid_credentials: "Incorrect email or password. Check your credentials and try again.",
        account_locked: "Account temporarily locked due to repeated failed attempts. Wait and retry.",
        account_inactive: "This account is inactive. Contact your organization administrator.",
        csrf_validation_failed: "Form security token expired. Please retry.",
        validation_error: "Enter both email and password before signing in.",
        upstream_unavailable: "Backend API service is unreachable. Ensure the API server is running.",
        unknown: "Unable to sign in right now. Please try again.",
      },
    },
    shell: {
      skipToMain: "Skip to main content",
      scopeUnboundBadge: "No Active Scope (Default-Deny)",
      scopeCompactBadge: "No active entity scope",
      scopeUnboundHint: "Contact an Organization Administrator to assign a tenant and entity grants.",
      scopeBoundHint: "Tenant and legal-entity scope are locked transaction-locally via PostgreSQL RLS.",
      selectScope: "Select Entity",
      switchScopeLabel: "Switch legal-entity workspace",
      scopeMenuHeading: "Authorized Legal Entities",
      openDirectoryLink: "Open Workspace Directory",
      activeTag: "Active",
      signOut: "Sign out",
      signingOut: "Signing out...",
      logoutError: "Failed to sign out cleanly. Try again.",
      navTitle: "Operational Modules",
      openMenu: "Open navigation menu",
      closeMenu: "Close navigation menu",
      navItems: {
        overview: "Session Overview",
        workspaces: "Entity Workspaces",
        procurement: "Procurement",
        inventory: "Inventory",
        sales: "Sales",
        finance: "Finance & GL",
        admin: "Administration",
      },
      lockedSuffix: "Not granted in active entity",
      permittedSuffix: "Permitted; opens in subsequent slice",
      modulePlannedHint: "Planned",
      sessionHeading: "Session Identity & Authorization Summary",
      sessionLead: "Account and active workspace assignments loaded live from PostgreSQL under least privilege.",
      recordCardTitle: "PostgreSQL Identity Record",
      fieldUser: "Display Name",
      fieldEmail: "Registered Email",
      fieldLocale: "Preferred Locale",
      fieldLastLogin: "Last Signed In (WIB)",
      fieldWorkspaceStatus: "Workspace Status",
      fieldAuthorizedCount: "Authorized Entities",
      fieldDbVerification: "Database Principal & Connection",
      dbVerifiedValue: "Live PostgreSQL verified (non-superuser runtime role + forced RLS)",
      boundaryTitle: "Multi-Tenant Isolation Boundary",
      boundaryBody:
        "Every scoped operation resolves server-side membership and entity grants before binding transaction-local app.tenant_id in PostgreSQL.",
      nextStepHeading: "Legal-Entity Workspaces",
      nextStepBodyActive:
        "Open the workspace directory to enter an authorized legal entity or switch entities cleanly without stale cache.",
      nextStepBodyEmpty:
        "No active tenant membership or entity grants are currently assigned to this account. Open the directory for provisioning details.",
      openDirectoryCta: "Open Workspace Directory",
      loadingSession: "Checking active session...",
      errorHeading: "Unable to Verify Session",
      errorRetry: "Retry",
      errorBackToLogin: "Back to Sign In",
    },
    workspaces: {
      heading: "Workspace Directory",
      lead: "Only tenants and legal entities with active membership and grants for your account are listed.",
      tenantCountLabel: "Active tenants",
      entityCountLabel: "Authorized entities",
      colCode: "Entity Code",
      colName: "Legal Entity Name",
      colEntity: "Legal Entity",
      colNpwp: "Tax ID (NPWP)",
      colFiscal: "Currency & Time Zone",
      colFiscalProfile: "Tax ID & Fiscal Context",
      colGrants: "Active Grants",
      colAction: "Action",
      enterWorkspace: "Enter Workspace",
      activeStatus: "Active",
      noNpwp: "Not registered",
      emptyTitle: "No Authorized Workspaces Assigned",
      emptyBody:
        "Your ASP.NET Core Identity account is verified, but you hold zero active tenant memberships or legal-entity access grants. Access remains denied by default.",
      emptyIdentityRef: "Authenticated Identity Reference",
      emptyGuidanceTitle: "Provisioning Workspace Access",
      emptyGuidanceStep1: "Ask an Organization Administrator to add your account email as a tenant member and grant legal-entity permissions.",
      emptyGuidanceStep2: "Invitation and four-eyes administration flows arrive in S02.",
    },
    entitySheet: {
      backToDirectory: "Workspace Directory",
      contextCardTitle: "Legal Entity Profile & Fiscal Context",
      fieldTenant: "Parent Tenant",
      fieldEntityCode: "Entity Code",
      fieldEntityName: "Registered Name",
      fieldNpwp: "Tax ID (NPWP)",
      fieldCurrency: "Functional Currency",
      fieldCountryTz: "Jurisdiction & Time Zone",
      fieldEvaluatedAt: "Server Verification (WIB)",
      permissionsCardTitle: "Effective Permissions & Module Readiness",
      colModule: "Operational Module",
      colPermission: "Permission Code",
      colReadiness: "Readiness State",
      availableState: "Available (S01)",
      permittedPlannedPrefix: "Permitted · Stage",
      notGrantedState: "Not Granted",
      loadingEntity: "Loading isolated entity workspace...",
      unavailableTitle: "Workspace Scope Unavailable",
      unavailableBody:
        "The requested tenant or legal entity does not exist or is outside your active authorization.",
      forbiddenTitle: "Insufficient Entity Permission",
      forbiddenBody:
        "You can access this legal entity, but your active grants do not include the required permission.",
      returnWorkspaces: "Return to Workspace Directory",
      moduleNames: {
        overview: "Entity Overview & Governance",
        procurement: "Procurement (PO, GRN, Vendors)",
        inventory: "Inventory & Stock Ledger",
        sales: "Sales & Invoicing",
        finance: "Finance, Indonesian Tax & GL",
        admin: "Organization & Access Administration",
      },
    },
  },
};

export function formatJakartaDateTime(isoUtc: string | null | undefined, locale: SupportedLocale): string {
  if (!isoUtc) return "—";
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "medium",
    timeZone: "Asia/Jakarta",
  }).format(date);
}

export function formatIdr(amount: number): string {
  return new Intl.NumberFormat("id-ID", {
    style: "currency",
    currency: "IDR",
    maximumFractionDigits: 2,
  }).format(amount);
}
