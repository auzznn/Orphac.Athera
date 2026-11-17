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
      invitations: string;
      partners: string;
      catalog: string;
      approvals: string;
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
    fieldMfaStatus: string;
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
  mfa: {
    cardTitle: string;
    enabledBadge: string;
    disabledBadge: string;
    description: string;
    setupButton: string;
    setupBusy: string;
    sharedKeyLabel: string;
    codeLabel: string;
    codePlaceholder: string;
    verifyButton: string;
    verifyBusy: string;
    verifiedNotice: string;
    codeRequiredError: string;
  };
  invitations: {
    heading: string;
    lead: string;
    cardTitle: string;
    tokenLabel: string;
    tokenPlaceholder: string;
    submitButton: string;
    submitBusy: string;
    successTitle: string;
    successBody: string;
    enterGrantedEntity: string;
    tokenRequiredError: string;
    errors: Record<string, string>;
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
    openInvitationCta: string;
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
  approvals: {
    heading: string;
    lead: string;
    requestCardTitle: string;
    policyLabel: string;
    permissionLabel: string;
    reasonLabel: string;
    reasonPlaceholder: string;
    submitButton: string;
    submitBusy: string;
    casesCardTitle: string;
    emptyCases: string;
    colCase: string;
    colRequester: string;
    colPolicy: string;
    colStatus: string;
    colDecisionAction: string;
    statusPending: string;
    statusApproved: string;
    statusRejected: string;
    selfApprovalNotice: string;
    mfaRequiredTag: string;
    reasonRequiredError: string;
    decisionReasonPlaceholder: string;
    decisionReasonRequired: string;
    mfaEnrollPrompt: string;
    mfaEnrollLink: string;
    decidedPrefix: string;
    approveButton: string;
    rejectButton: string;
    decidingBusy: string;
    errors: Record<string, string>;
  };
  admin: {
    heading: string;
    lead: string;
    outboxCardTitle: string;
    outboxPending: string;
    outboxProcessed: string;
    outboxFailed: string;
    dispatchOutboxButton: string;
    dispatchBusy: string;
    membersCardTitle: string;
    colMember: string;
    colMembershipState: string;
    colGrants: string;
    colMemberActions: string;
    addPermissionLabel: string;
    grantButton: string;
    suspendButton: string;
    revokeButton: string;
    mfaActiveTag: string;
    mfaOffTag: string;
    selfLockedHint: string;
    statusLabels: Record<string, string>;
    inviteCardTitle: string;
    inviteEmailLabel: string;
    inviteEmailPlaceholder: string;
    inviteEmailRequiredError: string;
    inviteRoleLabel: string;
    inviteExpiryLabel: string;
    createInviteButton: string;
    createInviteBusy: string;
    tokenCreatedTitle: string;
    tokenCreatedNote: string;
    copyTokenButton: string;
    copiedTokenLabel: string;
    openRedeemLink: string;
    revokeInviteButton: string;
    colInviteEmail: string;
    colInviteRole: string;
    colInviteStatus: string;
    colInviteExpiry: string;
    colInviteAction: string;
    auditCardTitle: string;
    colAuditTime: string;
    colAuditEvent: string;
    colAuditActor: string;
    colAuditSummary: string;
    emptyAudit: string;
    errors: Record<string, string>;
  };
  partners: {
    heading: string;
    lead: string;
    backToEntity: string;
    readOnlyNotice: string;
    od07MetadataNotice: string;
    filterCardTitle: string;
    searchLabel: string;
    searchPlaceholder: string;
    roleFilterLabel: string;
    roleFilterAll: string;
    roleFilterSupplier: string;
    roleFilterCustomer: string;
    statusFilterLabel: string;
    statusFilterAll: string;
    eligibleOnlyLabel: string;
    createCardTitle: string;
    codeLabel: string;
    codePlaceholder: string;
    legalNameLabel: string;
    legalNamePlaceholder: string;
    displayNameLabel: string;
    displayNamePlaceholder: string;
    rolesLabel: string;
    roleSupplierCheck: string;
    roleCustomerCheck: string;
    npwpLabel: string;
    npwpPlaceholder: string;
    pkpLabel: string;
    scopeModeLabel: string;
    createButton: string;
    createBusy: string;
    listCardTitle: string;
    emptyList: string;
    colCodeName: string;
    colRoles: string;
    colScopeTax: string;
    colEligibility: string;
    colStatusAction: string;
    inspectButton: string;
    viewDetailsButton: string;
    roleBothBadge: string;
    roleSupplierBadge: string;
    roleCustomerBadge: string;
    supplierEligibleTag: string;
    customerEligibleTag: string;
    ineligibleTag: string;
    detailCardTitle: string;
    deactivateButton: string;
    activateButton: string;
    scopeAllOption: string;
    scopeRestrictedOption: string;
    updateScopeLabel: string;
    updateScopeButton: string;
    contactsTitle: string;
    emptyContacts: string;
    contactNameLabel: string;
    contactNamePlaceholder: string;
    contactRoleLabel: string;
    contactEmailLabel: string;
    contactEmailPlaceholder: string;
    contactPhoneLabel: string;
    contactPhonePlaceholder: string;
    contactPrimaryLabel: string;
    addContactButton: string;
    addContactBusy: string;
    profileTitle: string;
    profileSupplierCheck: string;
    profileCustomerCheck: string;
    profileTermsLabel: string;
    profileNotesLabel: string;
    profileNotesPlaceholder: string;
    saveProfileButton: string;
    saveProfileBusy: string;
    statusLabels: Record<string, string>;
    scopeLabels: Record<string, string>;
    pkpLabels: Record<string, string>;
    contactRoleLabels: Record<string, string>;
    validation: {
      codeRequired: string;
      legalNameRequired: string;
      roleRequired: string;
      contactNameRequired: string;
      profileTermsInvalid: string;
    };
    errors: Record<string, string>;
  };
  catalog: {
    heading: string;
    lead: string;
    backToEntity: string;
    readOnlyNotice: string;
    uomCardTitle: string;
    uomCodeLabel: string;
    uomCodePlaceholder: string;
    uomNameLabel: string;
    uomNamePlaceholder: string;
    uomScaleLabel: string;
    createUomButton: string;
    createUomBusy: string;
    scaleDiscreteHint: string;
    createItemCardTitle: string;
    itemCodeLabel: string;
    itemCodePlaceholder: string;
    itemNameLabel: string;
    itemNamePlaceholder: string;
    itemKindLabel: string;
    baseUomLabel: string;
    purchaseUomLabel: string;
    salesUomLabel: string;
    scopeModeLabel: string;
    createItemButton: string;
    createItemBusy: string;
    filterCardTitle: string;
    searchLabel: string;
    searchPlaceholder: string;
    kindFilterLabel: string;
    kindFilterAll: string;
    statusFilterLabel: string;
    statusFilterAll: string;
    eligibleOnlyLabel: string;
    listCardTitle: string;
    emptyList: string;
    colItem: string;
    colKind: string;
    colUoms: string;
    colEligibility: string;
    colAction: string;
    inspectButton: string;
    viewItemButton: string;
    purchaseEligibleTag: string;
    salesEligibleTag: string;
    ineligibleTag: string;
    detailCardTitle: string;
    deactivateButton: string;
    activateButton: string;
    toggleScopeButton: string;
    conversionsTitle: string;
    baseImplicitNote: string;
    emptyConversions: string;
    convUomLabel: string;
    convFactorLabel: string;
    convFactorPlaceholder: string;
    addConvButton: string;
    addConvBusy: string;
    converterTitle: string;
    converterUomLabel: string;
    converterQtyLabel: string;
    converterQtyPlaceholder: string;
    convertButton: string;
    convertBusy: string;
    convertResultLabel: string;
    convertRoundTripLabel: string;
    policyTitle: string;
    policyPurchaseCheck: string;
    policySalesCheck: string;
    policyNotesLabel: string;
    policyNotesPlaceholder: string;
    savePolicyButton: string;
    savePolicyBusy: string;
    statusLabels: Record<string, string>;
    kindLabels: Record<string, string>;
    scopeLabels: Record<string, string>;
    validation: {
      uomCodeRequired: string;
      uomNameRequired: string;
      itemCodeRequired: string;
      itemNameRequired: string;
      factorInvalid: string;
      quantityInvalid: string;
    };
    errors: Record<string, string>;
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
        invitations: "Terima Undangan",
        partners: "Mitra Bisnis",
        catalog: "Katalog & Satuan",
        approvals: "Persetujuan",
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
      fieldMfaStatus: "Autentikator MFA (TOTP)",
      fieldDbVerification: "Koneksi & Peran Basis Data",
      dbVerifiedValue: "Terhubung ke PostgreSQL (peran runtime non-superuser + RLS aktif)",
      boundaryTitle: "Batas Keamanan Multi-Tenant Aktif",
      boundaryBody:
        "Setiap permintaan ruang kerja memvalidasi keanggotaan dan izin entitas di server sebelum mengunci app.tenant_id lokal transaksi.",
      nextStepHeading: "Ruang Kerja Entitas Hukum",
      nextStepBodyActive:
        "Buka direktori ruang kerja untuk masuk ke entitas hukum yang Anda miliki atau berpindah konteks secara aman.",
      nextStepBodyEmpty:
        "Akun ini belum memiliki keanggotaan tenant atau izin entitas aktif. Gunakan token undangan atau hubungi admin organisasi.",
      openDirectoryCta: "Buka Direktori Ruang Kerja",
      loadingSession: "Memeriksa sesi aktif...",
      errorHeading: "Gagal Memverifikasi Sesi",
      errorRetry: "Coba Lagi",
      errorBackToLogin: "Kembali ke Masuk",
    },
    mfa: {
      cardTitle: "Keamanan MFA TOTP (Persetujuan Berisiko Tinggi)",
      enabledBadge: "MFA Aktif",
      disabledBadge: "MFA Belum Aktif",
      description: "Diwajibkan untuk menyetujui kebijakan persetujuan berisiko tinggi pada entitas.",
      setupButton: "Siapkan Kunci TOTP",
      setupBusy: "Membuat kunci...",
      sharedKeyLabel: "Kunci Rahasia Authenticator (Base32)",
      codeLabel: "Kode 6 Digit Authenticator",
      codePlaceholder: "123456",
      verifyButton: "Verifikasi & Aktifkan MFA",
      verifyBusy: "Memverifikasi...",
      verifiedNotice: "MFA TOTP aktif untuk akun ini.",
      codeRequiredError: "Masukkan kode 6 digit dari aplikasi Authenticator.",
    },
    invitations: {
      heading: "Terima Undangan Organisasi",
      lead: "Masukkan token undangan sekali pakai dari Administrator Organisasi menggunakan akun email yang cocok.",
      cardTitle: "Aktivasi Undangan Entitas Hukum",
      tokenLabel: "Token Undangan",
      tokenPlaceholder: "Tempel token undangan di sini",
      submitButton: "Terima Undangan & Aktifkan Akses",
      submitBusy: "Memvalidasi undangan...",
      successTitle: "Undangan Diterima",
      successBody: "Keanggotaan dan izin entitas baru telah diaktifkan tanpa mengaktifkan ulang izin lama yang pernah dicabut.",
      enterGrantedEntity: "Masuk ke Entitas Sekarang",
      tokenRequiredError: "Masukkan token undangan sekali pakai dari administrator.",
      errors: {
        invitation_not_found: "Token undangan tidak dikenali.",
        invitation_expired: "Undangan telah kedaluwarsa. Minta administrator membuat undangan baru.",
        invitation_revoked: "Undangan telah dicabut oleh administrator.",
        invitation_already_accepted: "Undangan ini sudah digunakan sebelumnya.",
        invitation_email_mismatch: "Email akun yang sedang masuk tidak cocok dengan penerima undangan.",
      },
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
      emptyGuidanceStep1: "Minta Administrator Organisasi membuat undangan pada menu Administrasi entitas untuk email akun Anda.",
      emptyGuidanceStep2: "Setelah menerima token undangan, buka halaman Terima Undangan untuk mengaktifkan akses entitas.",
      openInvitationCta: "Masukkan Token Undangan",
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
      availableState: "Tersedia (S01–S03)",
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
        partners: "Mitra Pemasok & Pelanggan",
        catalog: "Katalog Barang & Satuan UoM",
        approvals: "Kotak Masuk & Persetujuan Empat Mata",
        procurement: "Pengadaan (PO, GRN, Pemasok)",
        inventory: "Persediaan & Buku Besar Stok",
        sales: "Penjualan & Penagihan",
        finance: "Keuangan, Pajak PPN/PPh & GL",
        admin: "Administrasi Organisasi, Undangan & Audit",
      },
    },
    approvals: {
      heading: "Kotak Masuk Persetujuan Empat Mata",
      lead: "Ajukan permintaan izin entitas atau putuskan permintaan tertunda secara independen (OD-02: pengaju tidak boleh menyetujui permintaannya sendiri).",
      requestCardTitle: "Ajukan Permintaan Izin Entitas",
      policyLabel: "Kebijakan Persetujuan",
      permissionLabel: "Izin yang Diminta",
      reasonLabel: "Justifikasi Operasional",
      reasonPlaceholder: "Contoh: Perlu akses stok Q4",
      submitButton: "Kirim Permintaan Persetujuan",
      submitBusy: "Mengirim permintaan...",
      casesCardTitle: "Daftar Kasus Persetujuan Entitas",
      emptyCases: "Belum ada kasus persetujuan pada entitas ini.",
      colCase: "Permintaan & Kebijakan",
      colRequester: "Pengaju & Pemutus",
      colPolicy: "Aturan Kebijakan",
      colStatus: "Status Keputusan",
      colDecisionAction: "Keputusan Independen",
      statusPending: "Menunggu Keputusan",
      statusApproved: "Disetujui",
      statusRejected: "Ditolak",
      selfApprovalNotice: "Aturan Empat Mata (OD-02): Anda adalah pengaju kasus ini; persetujuan hanya dapat dilakukan oleh pemutus lain.",
      mfaRequiredTag: "Wajib MFA",
      reasonRequiredError: "Tuliskan justifikasi operasional sebelum mengirim permintaan.",
      decisionReasonPlaceholder: "Alasan persetujuan / penolakan...",
      decisionReasonRequired: "Tuliskan alasan keputusan sebelum menyetujui atau menolak.",
      mfaEnrollPrompt: "Kebijakan ini mewajibkan MFA TOTP aktif sebelum Anda dapat memutuskan permintaan.",
      mfaEnrollLink: "Aktifkan MFA di halaman Akun",
      decidedPrefix: "Diputuskan",
      approveButton: "Setujui",
      rejectButton: "Tolak",
      decidingBusy: "Memproses...",
      errors: {
        four_eyes_self_approval_forbidden: "Aturan Empat Mata melarang menyetujui permintaan Anda sendiri.",
        mfa_required: "Kebijakan ini mewajibkan akun pemutus mengaktifkan MFA TOTP terlebih dahulu.",
        case_already_decided: "Kasus persetujuan ini sudah diputuskan sebelumnya.",
      },
    },
    admin: {
      heading: "Administrasi Entitas, Undangan & Audit",
      lead: "Kelola anggota tenant, undangan sekali pakai, izin per entitas, log audit append-only, dan pengiriman outbox.",
      outboxCardTitle: "Status Outbox Transaksional Tenant",
      outboxPending: "Tertunda",
      outboxProcessed: "Diproses",
      outboxFailed: "Gagal",
      dispatchOutboxButton: "Proses Antrean Outbox",
      dispatchBusy: "Memproses outbox...",
      membersCardTitle: "Anggota Tenant & Izin Entitas",
      colMember: "Pengguna & MFA",
      colMembershipState: "Status Keanggotaan",
      colGrants: "Izin Entitas Ini",
      colMemberActions: "Tindakan Administrasi",
      addPermissionLabel: "Tambah izin",
      grantButton: "Berikan",
      suspendButton: "Tangguhkan",
      revokeButton: "Cabut Akses",
      mfaActiveTag: "MFA Aktif",
      mfaOffTag: "Tanpa MFA",
      selfLockedHint: "Akun sendiri",
      statusLabels: {
        ACTIVE: "Aktif",
        SUSPENDED: "Ditangguhkan",
        REVOKED: "Dicabut",
        PENDING: "Menunggu",
        ACCEPTED: "Diterima",
        EXPIRED: "Kedaluwarsa",
      },
      inviteCardTitle: "Undang Pengguna ke Entitas Ini",
      inviteEmailLabel: "Email Penerima",
      inviteEmailPlaceholder: "staf@demo.athera.local",
      inviteEmailRequiredError: "Masukkan alamat email penerima yang valid.",
      inviteRoleLabel: "Templat Peran",
      inviteExpiryLabel: "Masa Berlaku (Jam)",
      createInviteButton: "Buat Undangan",
      createInviteBusy: "Membuat undangan...",
      tokenCreatedTitle: "Token Undangan Sekali Pakai Dibuat",
      tokenCreatedNote: "Salin token atau tautan ini sekarang (server hanya menyimpan hash SHA-256):",
      copyTokenButton: "Salin Token",
      copiedTokenLabel: "Tersalin",
      openRedeemLink: "Buka halaman penukaran token",
      revokeInviteButton: "Cabut Undangan",
      colInviteEmail: "Email Undangan",
      colInviteRole: "Peran",
      colInviteStatus: "Status",
      colInviteExpiry: "Kedaluwarsa (WIB)",
      colInviteAction: "Tindakan",
      auditCardTitle: "Jejak Audit Append-Only Entitas",
      colAuditTime: "Waktu (WIB)",
      colAuditEvent: "Peristiwa",
      colAuditActor: "Aktor",
      colAuditSummary: "Ringkasan",
      emptyAudit: "Belum ada catatan audit tercatat.",
      errors: {
        invalid_permission_code: "Kode izin tidak terdaftar dalam katalog resmi.",
        grant_already_exists: "Izin tersebut sudah aktif dimiliki pengguna.",
      },
    },
    partners: {
      heading: "Mitra Bisnis (Pemasok & Pelanggan)",
      lead: "Data induk mitra dipakai bersama per tenant, sedangkan kelayakan operasional dikunci per entitas hukum.",
      backToEntity: "Ringkasan Entitas",
      readOnlyNotice: "Mode baca saja: Anda memiliki izin lihat (partners.read) tanpa hak ubah (partners.manage).",
      od07MetadataNotice: "Catatan kebijakan OD-07: NPWP dan status PKP dicatat sebagai metadata master saja; perhitungan tarif PPN/PPh ditangani terpisah pada modul pajak (S04+).",
      filterCardTitle: "Cari & Saring Mitra Bisnis",
      searchLabel: "Cari Kode / Nama",
      searchPlaceholder: "Contoh: PT-DUAL-01 atau Mitra Niaga",
      roleFilterLabel: "Peran Mitra",
      roleFilterAll: "Semua Peran",
      roleFilterSupplier: "Pemasok",
      roleFilterCustomer: "Pelanggan",
      statusFilterLabel: "Status",
      statusFilterAll: "Semua Status",
      eligibleOnlyLabel: "Hanya layak di entitas ini",
      createCardTitle: "Daftarkan Mitra Bisnis Baru",
      codeLabel: "Kode Mitra",
      codePlaceholder: "PT-DAGANG-01",
      legalNameLabel: "Nama Badan Hukum",
      legalNamePlaceholder: "PT Dagang Serba Usaha Nusantara",
      displayNameLabel: "Nama Tampilan",
      displayNamePlaceholder: "Dagang Serba Usaha",
      rolesLabel: "Peran Transaksi",
      roleSupplierCheck: "Pemasok (Supplier)",
      roleCustomerCheck: "Pelanggan (Customer)",
      npwpLabel: "NPWP (Opsional)",
      npwpPlaceholder: "01.333.444.5-011.000",
      pkpLabel: "Metadata PKP",
      scopeModeLabel: "Cakupan Entitas",
      createButton: "Simpan Mitra Baru",
      createBusy: "Menyimpan mitra...",
      listCardTitle: "Daftar Mitra Bisnis Tenant",
      emptyList: "Tidak ada mitra yang sesuai dengan filter.",
      colCodeName: "Kode & Nama Mitra",
      colRoles: "Peran",
      colScopeTax: "Cakupan & Metadata PKP",
      colEligibility: "Kelayakan Entitas Aktif",
      colStatusAction: "Status & Rincian",
      inspectButton: "Kelola Rincian",
      viewDetailsButton: "Lihat Rincian",
      roleBothBadge: "Pemasok & Pelanggan",
      roleSupplierBadge: "Pemasok",
      roleCustomerBadge: "Pelanggan",
      supplierEligibleTag: "Bisa Beli (AP)",
      customerEligibleTag: "Bisa Jual (AR)",
      ineligibleTag: "Tidak Layak di Entitas Ini",
      detailCardTitle: "Rincian Mitra, Kontak & Kelayakan Entitas",
      deactivateButton: "Nonaktifkan Mitra",
      activateButton: "Aktifkan Kembali",
      scopeAllOption: "Semua Entitas Aktif",
      scopeRestrictedOption: "Entitas Terpilih Saja",
      updateScopeLabel: "Ubah Mode Cakupan",
      updateScopeButton: "Terapkan Mode Cakupan",
      contactsTitle: "Daftar Kontak Mitra",
      emptyContacts: "Belum ada kontak terdaftar untuk mitra ini.",
      contactNameLabel: "Nama Kontak",
      contactNamePlaceholder: "Dian Permata",
      contactRoleLabel: "Peran Kontak",
      contactEmailLabel: "Email Kontak",
      contactEmailPlaceholder: "dian@mitra.co.id",
      contactPhoneLabel: "Telepon",
      contactPhonePlaceholder: "+62-21-555-0199",
      contactPrimaryLabel: "Kontak utama",
      addContactButton: "Tambah Kontak",
      addContactBusy: "Menambah kontak...",
      profileTitle: "Kelayakan Entitas Hukum Saat Ini",
      profileSupplierCheck: "Izinkan sebagai Pemasok pada entitas ini",
      profileCustomerCheck: "Izinkan sebagai Pelanggan pada entitas ini",
      profileTermsLabel: "Termin Pembayaran (Hari)",
      profileNotesLabel: "Catatan Kebijakan Entitas",
      profileNotesPlaceholder: "Contoh: Pemasok resmi gudang Jakarta",
      saveProfileButton: "Simpan Profil Entitas",
      saveProfileBusy: "Menyimpan profil...",
      statusLabels: {
        ACTIVE: "Aktif",
        INACTIVE: "Nonaktif",
      },
      scopeLabels: {
        ALL_ENTITIES: "Semua Entitas",
        RESTRICTED_ENTITIES: "Terbatas per Entitas",
      },
      pkpLabels: {
        UNKNOWN: "Belum Diketahui",
        PKP: "PKP (Metadata)",
        NON_PKP: "Non-PKP",
      },
      contactRoleLabels: {
        COMMERCIAL: "Komersial / Sales",
        BILLING: "Penagihan / Pajak",
        RECEIVING_DISPATCH: "Gudang / Penerimaan",
      },
      validation: {
        codeRequired: "Masukkan kode mitra (2–32 karakter huruf besar/angka).",
        legalNameRequired: "Masukkan nama badan hukum mitra.",
        roleRequired: "Pilih minimal satu peran: Pemasok atau Pelanggan.",
        contactNameRequired: "Masukkan nama kontak terlebih dahulu.",
        profileTermsInvalid: "Termin pembayaran harus bilangan bulat 0–365 hari.",
      },
      errors: {
        duplicate_partner_code: "Kode mitra sudah digunakan dalam tenant ini.",
        partner_role_incompatible: "Kelayakan entitas tidak boleh melampaui peran dasar mitra (Pemasok/Pelanggan).",
        concurrency_conflict: "Data mitra diubah pengguna lain. Muat ulang terlebih dahulu.",
        insufficient_entity_permission: "Anda tidak memiliki izin partners.manage pada entitas ini.",
      },
    },
    catalog: {
      heading: "Katalog Barang & Satuan UoM",
      lead: "Kelola kamus satuan terukur, katalog barang tanpa saldo stok, faktor konversi desimal eksak, dan kelayakan per entitas.",
      backToEntity: "Ringkasan Entitas",
      readOnlyNotice: "Mode baca saja: Anda memiliki izin lihat (catalog.read) tanpa hak ubah (catalog.manage).",
      uomCardTitle: "Kamus Satuan Ukur (UoM)",
      uomCodeLabel: "Kode UoM",
      uomCodePlaceholder: "CTN",
      uomNameLabel: "Nama Satuan",
      uomNamePlaceholder: "Karton Induk",
      uomScaleLabel: "Skala Desimal",
      createUomButton: "Tambah Satuan",
      createUomBusy: "Menyimpan satuan...",
      scaleDiscreteHint: "0 = diskrit utuh (seperti PCS); 1–6 = kontinu (seperti KG)",
      createItemCardTitle: "Daftarkan Barang / Jasa Baru",
      itemCodeLabel: "Kode Barang",
      itemCodePlaceholder: "ITM-KRD-01",
      itemNameLabel: "Nama Barang / Jasa",
      itemNamePlaceholder: "Kardus Induk 60x40x40 cm",
      itemKindLabel: "Jenis Item",
      baseUomLabel: "Satuan Dasar (Base)",
      purchaseUomLabel: "Satuan Beli Default",
      salesUomLabel: "Satuan Jual Default",
      scopeModeLabel: "Cakupan Entitas",
      createItemButton: "Simpan Item Katalog",
      createItemBusy: "Menyimpan item...",
      filterCardTitle: "Cari & Saring Katalog",
      searchLabel: "Cari Kode / Nama Barang",
      searchPlaceholder: "Contoh: ITM-PKG-001 atau Karton",
      kindFilterLabel: "Jenis Item",
      kindFilterAll: "Semua Jenis",
      statusFilterLabel: "Status",
      statusFilterAll: "Semua Status",
      eligibleOnlyLabel: "Hanya diizinkan di entitas ini",
      listCardTitle: "Daftar Barang & Satuan Tenant",
      emptyList: "Tidak ada item katalog yang cocok dengan filter.",
      colItem: "Kode & Nama Item",
      colKind: "Jenis",
      colUoms: "Base · Beli · Jual",
      colEligibility: "Kebijakan Entitas",
      colAction: "Status & Rincian",
      inspectButton: "Kelola UoM & Kebijakan",
      viewItemButton: "Lihat UoM & Kebijakan",
      purchaseEligibleTag: "Beli Diizinkan",
      salesEligibleTag: "Jual Diizinkan",
      ineligibleTag: "Diblokir di Entitas Ini",
      detailCardTitle: "Konversi UoM Eksak & Kebijakan Entitas",
      deactivateButton: "Nonaktifkan Item",
      activateButton: "Aktifkan Kembali",
      toggleScopeButton: "Terapkan Mode Cakupan",
      conversionsTitle: "Aturan Konversi ke Satuan Dasar",
      baseImplicitNote: "Satuan dasar selalu bernilai faktor 1 secara implisit.",
      emptyConversions: "Belum ada satuan alternatif tambahan (menggunakan satuan dasar 1:1).",
      convUomLabel: "Satuan Sumber",
      convFactorLabel: "Faktor ke Satuan Dasar",
      convFactorPlaceholder: "24",
      addConvButton: "Simpan Konversi",
      addConvBusy: "Menyimpan...",
      converterTitle: "Uji Konversi Desimal Eksak (Server Verified)",
      converterUomLabel: "Dari Satuan",
      converterQtyLabel: "Kuantitas Sumber",
      converterQtyPlaceholder: "3",
      convertButton: "Hitung ke Satuan Dasar",
      convertBusy: "Menghitung...",
      convertResultLabel: "Hasil Dasar Eksak",
      convertRoundTripLabel: "Balik ke Satuan Asal",
      policyTitle: "Kebijakan Penggunaan pada Entitas Ini",
      policyPurchaseCheck: "Izinkan Pengadaan (PO) pada entitas ini",
      policySalesCheck: "Izinkan Penjualan (SO) pada entitas ini",
      policyNotesLabel: "Catatan Kebijakan",
      policyNotesPlaceholder: "Contoh: Aktif untuk gudang Jakarta",
      savePolicyButton: "Simpan Kebijakan Entitas",
      savePolicyBusy: "Menyimpan kebijakan...",
      statusLabels: {
        ACTIVE: "Aktif",
        INACTIVE: "Nonaktif",
      },
      kindLabels: {
        STOCKED_GOOD: "Barang Stok",
        NON_STOCK_SERVICE: "Jasa Non-Stok",
      },
      scopeLabels: {
        ALL_ENTITIES: "Semua Entitas",
        RESTRICTED_ENTITIES: "Terbatas per Entitas",
      },
      validation: {
        uomCodeRequired: "Masukkan kode UoM (1–16 karakter).",
        uomNameRequired: "Masukkan nama satuan ukur.",
        itemCodeRequired: "Masukkan kode barang (2–32 karakter).",
        itemNameRequired: "Masukkan nama barang.",
        factorInvalid: "Faktor konversi harus lebih besar dari nol.",
        quantityInvalid: "Masukkan kuantitas numerik lebih dari nol.",
      },
      errors: {
        duplicate_uom_code: "Kode satuan UoM sudah ada dalam tenant ini.",
        duplicate_item_code: "Kode item sudah digunakan dalam tenant ini.",
        base_uom_conversion_not_needed: "Satuan dasar selalu memiliki faktor konversi implisit 1.",
        fractional_discrete_base_quantity: "Kuantitas menghasilkan pecahan untuk satuan dasar diskrit (skala 0).",
        unsupported_uom_conversion: "Tidak ada konversi aktif antara item ini dan satuan tersebut.",
        concurrency_conflict: "Item diubah pengguna lain. Muat ulang terlebih dahulu.",
        insufficient_entity_permission: "Anda tidak memiliki izin catalog.manage pada entitas ini.",
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
        invitations: "Accept Invitation",
        partners: "Partners",
        catalog: "Catalogue & UoM",
        approvals: "Approvals",
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
      fieldMfaStatus: "TOTP MFA Protection",
      fieldDbVerification: "Database Principal & Connection",
      dbVerifiedValue: "Live PostgreSQL verified (non-superuser runtime role + forced RLS)",
      boundaryTitle: "Multi-Tenant Isolation Boundary",
      boundaryBody:
        "Every scoped operation resolves server-side membership and entity grants before binding transaction-local app.tenant_id in PostgreSQL.",
      nextStepHeading: "Legal-Entity Workspaces",
      nextStepBodyActive:
        "Open the workspace directory to enter an authorized legal entity or switch entities cleanly without stale cache.",
      nextStepBodyEmpty:
        "No active tenant membership or entity grants are assigned. Accept an invitation token or contact an admin.",
      openDirectoryCta: "Open Workspace Directory",
      loadingSession: "Checking active session...",
      errorHeading: "Unable to Verify Session",
      errorRetry: "Retry",
      errorBackToLogin: "Back to Sign In",
    },
    mfa: {
      cardTitle: "TOTP MFA Security (Elevated Approvals)",
      enabledBadge: "MFA Active",
      disabledBadge: "MFA Not Enabled",
      description: "Required before deciding approval cases under high-risk privilege escalation policy.",
      setupButton: "Generate TOTP Secret",
      setupBusy: "Generating secret...",
      sharedKeyLabel: "Authenticator Shared Secret (Base32)",
      codeLabel: "6-Digit Authenticator Code",
      codePlaceholder: "123456",
      verifyButton: "Verify & Enable MFA",
      verifyBusy: "Verifying...",
      verifiedNotice: "TOTP MFA is enabled on your account.",
      codeRequiredError: "Enter the 6-digit code from your authenticator app.",
    },
    invitations: {
      heading: "Accept Organization Invitation",
      lead: "Redeem a single-use invitation token issued by your Organization Administrator for this email.",
      cardTitle: "Legal-Entity Access Activation",
      tokenLabel: "Invitation Token",
      tokenPlaceholder: "Paste invitation token here",
      submitButton: "Accept Invitation & Activate Scope",
      submitBusy: "Validating token...",
      successTitle: "Invitation Accepted",
      successBody: "Your tenant membership and invited entity role grants are active without restoring any prior revoked grants.",
      enterGrantedEntity: "Open Granted Entity",
      tokenRequiredError: "Enter the single-use invitation token provided by your administrator.",
      errors: {
        invitation_not_found: "Invitation token was not found.",
        invitation_expired: "This invitation has expired. Ask an administrator for a new token.",
        invitation_revoked: "This invitation was revoked by an administrator.",
        invitation_already_accepted: "This invitation token has already been redeemed.",
        invitation_email_mismatch: "Your signed-in email does not match the invited recipient.",
      },
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
      emptyGuidanceStep1: "Ask an Organization Administrator to issue an entity invitation token for your account email.",
      emptyGuidanceStep2: "Once received, open Accept Invitation to activate scoped entity access.",
      openInvitationCta: "Redeem Invitation Token",
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
      availableState: "Available (S01–S03)",
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
        partners: "Business Partners (Suppliers & Customers)",
        catalog: "Item Catalogue & UoM Conversions",
        approvals: "Four-Eyes Approval Inbox",
        procurement: "Procurement (PO, GRN, Vendors)",
        inventory: "Inventory & Stock Ledger",
        sales: "Sales & Invoicing",
        finance: "Finance, Indonesian Tax & GL",
        admin: "Organization Administration & Audit",
      },
    },
    approvals: {
      heading: "Four-Eyes Approval Inbox",
      lead: "Submit scoped entity grant requests or review pending cases independently (OD-02: requester cannot approve own submission).",
      requestCardTitle: "Submit Entity Permission Request",
      policyLabel: "Approval Policy",
      permissionLabel: "Requested Permission",
      reasonLabel: "Business Justification",
      reasonPlaceholder: "e.g., Needed for Q4 stock audit",
      submitButton: "Submit Approval Request",
      submitBusy: "Submitting request...",
      casesCardTitle: "Entity Approval Cases",
      emptyCases: "No approval cases recorded for this legal entity yet.",
      colCase: "Request & Policy",
      colRequester: "Requester & Decider",
      colPolicy: "Policy",
      colStatus: "Status",
      colDecisionAction: "Independent Decision",
      statusPending: "Pending Decision",
      statusApproved: "Approved",
      statusRejected: "Rejected",
      selfApprovalNotice: "Four-Eyes Rule (OD-02): You submitted this request; only another authorized approver can decide it.",
      mfaRequiredTag: "MFA Required",
      reasonRequiredError: "Enter a business justification before submitting this request.",
      decisionReasonPlaceholder: "Written approval or rejection reason...",
      decisionReasonRequired: "Enter a written reason before approving or rejecting.",
      mfaEnrollPrompt: "This policy requires TOTP MFA enrollment before you can decide requests.",
      mfaEnrollLink: "Enable MFA on Account home",
      decidedPrefix: "Decided",
      approveButton: "Approve",
      rejectButton: "Reject",
      decidingBusy: "Deciding...",
      errors: {
        four_eyes_self_approval_forbidden: "Four-eyes rule prohibits deciding your own request.",
        mfa_required: "This policy requires TOTP MFA enrollment before deciding.",
        case_already_decided: "This approval case has already been decided.",
      },
    },
    admin: {
      heading: "Entity Administration, Invitations & Audit",
      lead: "Manage tenant memberships, single-use entity invitations, grants, append-only audit trail, and outbox processing.",
      outboxCardTitle: "Tenant Transactional Outbox Status",
      outboxPending: "Pending",
      outboxProcessed: "Processed",
      outboxFailed: "Failed",
      dispatchOutboxButton: "Process Outbox Queue",
      dispatchBusy: "Dispatching...",
      membersCardTitle: "Tenant Members & Entity Grants",
      colMember: "Member & MFA",
      colMembershipState: "Membership Status",
      colGrants: "Active Entity Grants",
      colMemberActions: "Administration Actions",
      addPermissionLabel: "Add grant",
      grantButton: "Grant",
      suspendButton: "Suspend",
      revokeButton: "Revoke Access",
      mfaActiveTag: "MFA Active",
      mfaOffTag: "No MFA",
      selfLockedHint: "Current actor",
      statusLabels: {
        ACTIVE: "Active",
        SUSPENDED: "Suspended",
        REVOKED: "Revoked",
        PENDING: "Pending",
        ACCEPTED: "Accepted",
        EXPIRED: "Expired",
      },
      inviteCardTitle: "Invite User to This Entity",
      inviteEmailLabel: "Recipient Email",
      inviteEmailPlaceholder: "staff@demo.athera.local",
      inviteEmailRequiredError: "Enter a valid recipient email address.",
      inviteRoleLabel: "Role Template",
      inviteExpiryLabel: "Expiry (Hours)",
      createInviteButton: "Create Invitation",
      createInviteBusy: "Issuing invitation...",
      tokenCreatedTitle: "One-Time Invitation Token Issued",
      tokenCreatedNote: "Copy this token or link now (server persists SHA-256 hash only):",
      copyTokenButton: "Copy Token",
      copiedTokenLabel: "Copied",
      openRedeemLink: "Open invitation redemption page",
      revokeInviteButton: "Revoke Invitation",
      colInviteEmail: "Invited Email",
      colInviteRole: "Role Template",
      colInviteStatus: "Status",
      colInviteExpiry: "Expires (WIB)",
      colInviteAction: "Action",
      auditCardTitle: "Append-Only Entity Audit Trail",
      colAuditTime: "Time (WIB)",
      colAuditEvent: "Event",
      colAuditActor: "Actor",
      colAuditSummary: "Summary",
      emptyAudit: "No audit events recorded yet.",
      errors: {
        invalid_permission_code: "Permission code is not in the approved catalog.",
        grant_already_exists: "Member already holds this active permission.",
      },
    },
    partners: {
      heading: "Business Partners (Suppliers & Customers)",
      lead: "Tenant-shared partner masters with per-legal-entity operational eligibility and metadata-only tax registration.",
      backToEntity: "Entity Summary",
      readOnlyNotice: "Read-only mode: you hold view permission (partners.read) without edit rights (partners.manage).",
      od07MetadataNotice: "OD-07 policy notice: NPWP and PKP status are stored strictly as master metadata; statutory Indonesian PPN/PPh rate determination applies in the Tax module (S04+).",
      filterCardTitle: "Search & Filter Partners",
      searchLabel: "Search Code / Name",
      searchPlaceholder: "e.g., PT-DUAL-01 or Mitra Niaga",
      roleFilterLabel: "Partner Role",
      roleFilterAll: "All Roles",
      roleFilterSupplier: "Suppliers",
      roleFilterCustomer: "Customers",
      statusFilterLabel: "Status",
      statusFilterAll: "All Statuses",
      eligibleOnlyLabel: "Eligible in active entity only",
      createCardTitle: "Register New Business Partner",
      codeLabel: "Partner Code",
      codePlaceholder: "PT-DAGANG-01",
      legalNameLabel: "Legal Entity Name",
      legalNamePlaceholder: "PT Dagang Serba Usaha Nusantara",
      displayNameLabel: "Display Name",
      displayNamePlaceholder: "Dagang Serba Usaha",
      rolesLabel: "Commercial Roles",
      roleSupplierCheck: "Supplier (Purchasing & AP)",
      roleCustomerCheck: "Customer (Sales & AR)",
      npwpLabel: "Tax ID / NPWP (Optional)",
      npwpPlaceholder: "01.333.444.5-011.000",
      pkpLabel: "PKP Metadata",
      scopeModeLabel: "Entity Scope",
      createButton: "Save New Partner",
      createBusy: "Saving partner...",
      listCardTitle: "Tenant Business Partners",
      emptyList: "No partners match the current filter criteria.",
      colCodeName: "Code & Partner Name",
      colRoles: "Roles",
      colScopeTax: "Scope & PKP Metadata",
      colEligibility: "Active Entity Eligibility",
      colStatusAction: "Status & Actions",
      inspectButton: "Inspect & Configure",
      viewDetailsButton: "View Details",
      roleBothBadge: "Supplier & Customer",
      roleSupplierBadge: "Supplier",
      roleCustomerBadge: "Customer",
      supplierEligibleTag: "Purchasing Eligible",
      customerEligibleTag: "Sales Eligible",
      ineligibleTag: "Ineligible Here",
      detailCardTitle: "Partner Detail, Contacts & Entity Eligibility",
      deactivateButton: "Deactivate Partner",
      activateButton: "Reactivate Partner",
      scopeAllOption: "All Active Entities",
      scopeRestrictedOption: "Restricted Entities Only",
      updateScopeLabel: "Entity Scope Policy",
      updateScopeButton: "Apply Scope Mode",
      contactsTitle: "Registered Contacts",
      emptyContacts: "No contacts recorded for this partner yet.",
      contactNameLabel: "Contact Name",
      contactNamePlaceholder: "Dian Permata",
      contactRoleLabel: "Contact Role",
      contactEmailLabel: "Contact Email",
      contactEmailPlaceholder: "dian@partner.co.id",
      contactPhoneLabel: "Phone",
      contactPhonePlaceholder: "+62-21-555-0199",
      contactPrimaryLabel: "Primary contact",
      addContactButton: "Add Contact",
      addContactBusy: "Adding contact...",
      profileTitle: "Current Legal-Entity Eligibility Profile",
      profileSupplierCheck: "Allow as Supplier in this entity",
      profileCustomerCheck: "Allow as Customer in this entity",
      profileTermsLabel: "Payment Terms (Days)",
      profileNotesLabel: "Entity Policy Notes",
      profileNotesPlaceholder: "e.g., Approved vendor for Jakarta hub",
      saveProfileButton: "Save Entity Profile",
      saveProfileBusy: "Saving profile...",
      statusLabels: {
        ACTIVE: "Active",
        INACTIVE: "Inactive",
      },
      scopeLabels: {
        ALL_ENTITIES: "All Entities",
        RESTRICTED_ENTITIES: "Restricted Entities",
      },
      pkpLabels: {
        UNKNOWN: "Unknown",
        PKP: "PKP (Metadata)",
        NON_PKP: "Non-PKP",
      },
      contactRoleLabels: {
        COMMERCIAL: "Commercial / Sales",
        BILLING: "Billing / Tax",
        RECEIVING_DISPATCH: "Receiving / Dispatch",
      },
      validation: {
        codeRequired: "Enter a partner code (2–32 uppercase characters).",
        legalNameRequired: "Enter the partner legal name.",
        roleRequired: "Select at least one role: Supplier or Customer.",
        contactNameRequired: "Enter a contact name before submitting.",
        profileTermsInvalid: "Payment terms must be an integer between 0 and 365 days.",
      },
      errors: {
        duplicate_partner_code: "Partner code already exists in this tenant.",
        partner_role_incompatible: "Entity eligibility cannot enable a role not assigned to this partner.",
        concurrency_conflict: "Partner was modified by another session. Reload before saving.",
        insufficient_entity_permission: "You do not hold partners.manage permission on this entity.",
      },
    },
    catalog: {
      heading: "Item Catalogue & Units of Measure",
      lead: "Maintain tenant units of measure, stock-free item definitions, exact decimal conversions, and per-entity purchase/sales policies.",
      backToEntity: "Entity Summary",
      readOnlyNotice: "Read-only mode: you hold view permission (catalog.read) without edit rights (catalog.manage).",
      uomCardTitle: "Unit of Measure Dictionary",
      uomCodeLabel: "UoM Code",
      uomCodePlaceholder: "CTN",
      uomNameLabel: "Unit Name",
      uomNamePlaceholder: "Master Carton",
      uomScaleLabel: "Decimal Scale",
      createUomButton: "Add UoM",
      createUomBusy: "Saving UoM...",
      scaleDiscreteHint: "0 = discrete integer units (e.g. PCS); 1–6 = continuous decimals (e.g. KG)",
      createItemCardTitle: "Register New Catalogue Item",
      itemCodeLabel: "Item Code",
      itemCodePlaceholder: "ITM-CTN-01",
      itemNameLabel: "Item / Service Name",
      itemNamePlaceholder: "Corrugated Shipping Carton 60x40x40 cm",
      itemKindLabel: "Item Kind",
      baseUomLabel: "Base UoM",
      purchaseUomLabel: "Default Purchase UoM",
      salesUomLabel: "Default Sales UoM",
      scopeModeLabel: "Entity Scope",
      createItemButton: "Save Catalogue Item",
      createItemBusy: "Saving item...",
      filterCardTitle: "Search & Filter Catalogue",
      searchLabel: "Search Code / Item Name",
      searchPlaceholder: "e.g., ITM-PKG-001 or Carton",
      kindFilterLabel: "Item Kind",
      kindFilterAll: "All Kinds",
      statusFilterLabel: "Status",
      statusFilterAll: "All Statuses",
      eligibleOnlyLabel: "Eligible in active entity only",
      listCardTitle: "Tenant Item Catalogue",
      emptyList: "No catalogue items match the current filter criteria.",
      colItem: "Code & Item Name",
      colKind: "Kind",
      colUoms: "Base · Purchase · Sales",
      colEligibility: "Active Entity Policy",
      colAction: "Status & Actions",
      inspectButton: "Configure UoM & Policy",
      viewItemButton: "View UoM & Policy",
      purchaseEligibleTag: "Purchase Allowed",
      salesEligibleTag: "Sales Allowed",
      ineligibleTag: "Blocked Here",
      detailCardTitle: "Exact UoM Conversions & Entity Policy",
      deactivateButton: "Deactivate Item",
      activateButton: "Reactivate Item",
      toggleScopeButton: "Apply Scope Mode",
      conversionsTitle: "Unit Conversions to Base UoM",
      baseImplicitNote: "Base UoM implicitly converts with factor 1.",
      emptyConversions: "No extra unit conversions configured yet (base 1:1 only).",
      convUomLabel: "Source UoM",
      convFactorLabel: "Factor to Base UoM",
      convFactorPlaceholder: "24",
      addConvButton: "Save Conversion",
      addConvBusy: "Saving...",
      converterTitle: "Verify Exact Decimal Conversion (Server-Evaluated)",
      converterUomLabel: "From Unit",
      converterQtyLabel: "Source Quantity",
      converterQtyPlaceholder: "3",
      convertButton: "Convert to Base UoM",
      convertBusy: "Converting...",
      convertResultLabel: "Exact Base Quantity",
      convertRoundTripLabel: "Round-Trip Check",
      policyTitle: "Current Legal-Entity Eligibility Policy",
      policyPurchaseCheck: "Allow Procurement (PO) in this entity",
      policySalesCheck: "Allow Sales (SO) in this entity",
      policyNotesLabel: "Policy Notes",
      policyNotesPlaceholder: "e.g., Active for Jakarta warehouse",
      savePolicyButton: "Save Entity Policy",
      savePolicyBusy: "Saving policy...",
      statusLabels: {
        ACTIVE: "Active",
        INACTIVE: "Inactive",
      },
      kindLabels: {
        STOCKED_GOOD: "Stocked Good",
        NON_STOCK_SERVICE: "Non-Stock Service",
      },
      scopeLabels: {
        ALL_ENTITIES: "All Entities",
        RESTRICTED_ENTITIES: "Restricted Entities",
      },
      validation: {
        uomCodeRequired: "Enter a unit code (1–16 characters).",
        uomNameRequired: "Enter the unit name.",
        itemCodeRequired: "Enter an item code (2–32 characters).",
        itemNameRequired: "Enter the item name.",
        factorInvalid: "Conversion factor must be strictly positive.",
        quantityInvalid: "Enter a positive numeric quantity.",
      },
      errors: {
        duplicate_uom_code: "Unit of measure code already exists in this tenant.",
        duplicate_item_code: "Item code already exists in this tenant.",
        base_uom_conversion_not_needed: "Base unit already has implicit factor 1.",
        fractional_discrete_base_quantity: "Conversion produces a fractional quantity for a discrete scale-0 base UoM.",
        unsupported_uom_conversion: "No active conversion is registered between this item and unit.",
        concurrency_conflict: "Item was modified by another session. Reload before saving.",
        insufficient_entity_permission: "You do not hold catalog.manage permission on this entity.",
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
