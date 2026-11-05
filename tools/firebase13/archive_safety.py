"""Producer archive preflight; consumer extraction remains stock XBD."""
import posixpath
import stat
import zipfile
from pathlib import PurePosixPath

def preflight(archive):
    names = set()
    files = set()
    links = {}
    with zipfile.ZipFile(archive) as source:
        for item in source.infolist():
            name = item.filename.rstrip('/')
            path = PurePosixPath(name)
            if (not name or path.is_absolute() or '\\' in name or ':' in name
                    or any(part in ('', '.', '..') for part in name.split('/'))):
                raise ValueError(f'Unsafe archive path: {item.filename}')
            if name.casefold() in names:
                raise ValueError(f'Archive destination collision: {name}')
            names.add(name.casefold())
            mode = stat.S_IFMT(item.external_attr >> 16)
            if mode not in (0, stat.S_IFREG, stat.S_IFDIR, stat.S_IFLNK):
                raise ValueError(f'Unsupported archive file type: {name}')
            if not item.is_dir():
                files.add(name.casefold())
            if stat.S_ISLNK(item.external_attr >> 16):
                target = source.read(item).decode('utf-8')
                if not target or target.startswith('/') or '\\' in target or ':' in target:
                    raise ValueError(f'Unsafe link target: {name} -> {target}')
                resolved = posixpath.normpath(posixpath.join(posixpath.dirname(name), target))
                if not resolved.startswith('Firebase/'):
                    raise ValueError(f'Escaping archive link: {name} -> {target}')
                links[name] = resolved
        for name in names:
            parents = PurePosixPath(name).parents
            if any(str(parent) in files for parent in parents):
                raise ValueError(f'Archive file/directory destination collision: {name}')
        for name in links:
            current = links[name]
            visited = {name}
            while current in links:
                if current in visited:
                    raise ValueError(f'Cyclic archive link: {name}')
                visited.add(current)
                current = links[current]
            # Never extract actual entries through a link declared by the archive.
            if any(other != name and other.startswith(name + '/') for other in source.namelist()):
                raise ValueError(f'Archive writes through a link: {name}')
