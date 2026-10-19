# PLACEHOLDER — zamenite ga Dockerfile-om vašeg servisa (npr. višefazni build za .NET / Node / Java / Python).
# Konvencije koje treba zadržati:
#   * stage 'runtime' = servis koji sluša na portu 8080 i odgovara na GET /health
#   * stage 'test'    = pokreće sve testove i vraća exit kod != 0 kada neki padne (koristi ga profil 'tests')

FROM busybox:1.36 AS test
CMD ["sh", "-c", "echo 'SCAFFOLD: dodajte testove i zamenite ovaj stage' && exit 1"]

FROM busybox:1.36 AS runtime
RUN mkdir /www && echo OK > /www/health
EXPOSE 8080
CMD ["httpd", "-f", "-p", "8080", "-h", "/www"]
