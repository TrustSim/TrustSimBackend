"""
=============================================================================
TrustSimBackend - Architecture Repo Map Generator (Aider Algorithm)
=============================================================================
Script tự động xây dựng bản đồ kiến trúc (Repo Map) cho toàn bộ backend C# .NET.
Cơ chế:
  1. Tree-sitter C# AST: Trích xuất Class, Interface, Record, Enum, Method signatures
     và các Attribute quan trọng ([HttpPost], [Route], [Authorize]).
  2. Dependency Graph: Xây dựng đồ thị phụ thuộc giữa các file dựa trên symbol reference.
  3. NetworkX PageRank: Xếp hạng độ ưu tiên (tầm ảnh hưởng) của các thành phần trong dự án.
  4. Tiktoken Budgeting: Định dạng cây thư mục tinh gọn, tối ưu vừa vặn context window của AI Agent.
=============================================================================
"""

import os
import sys
import argparse
from pathlib import Path
from collections import defaultdict
import networkx as nx
import tiktoken
import tree_sitter
import tree_sitter_c_sharp

# Đảm bảo in UTF-8 không lỗi trên Windows Console (cp1252)
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

# Khởi tạo Tree-sitter parser cho C#
CS_LANGUAGE = tree_sitter.Language(tree_sitter_c_sharp.language())
CS_PARSER = tree_sitter.Parser(CS_LANGUAGE)
TOKENIZER = tiktoken.get_encoding("cl100k_base")

IGNORED_DIRS = {
    "bin", "obj", ".git", ".vs", ".idea", 
    "test-ui", "docs", "keys", "migrations"
}

def parse_args():
    parser = argparse.ArgumentParser(description="Sinh Architecture Repo Map cho TrustSimBackend")
    parser.add_argument("--root", type=str, default=".", help="Thư mục gốc của repository")
    parser.add_argument("--output", type=str, default="docs/architecture/repo_map.md", help="Đường dẫn file đầu ra")
    parser.add_argument("--tokens", type=int, default=3500, help="Ngân sách token tối đa (mặc định: 3500)")
    parser.add_argument("--all-files", action="store_true", help="Hiển thị tất cả file thay vì cắt theo budget")
    return parser.parse_args()

def clean_signature(raw_text: str) -> str:
    """Làm sạch và chuẩn hóa signature dòng code, loại bỏ xuống dòng dư thừa."""
    lines = [line.strip() for line in raw_text.splitlines() if line.strip()]
    return " ".join(lines)

def extract_symbols_and_refs(file_path: Path):
    """
    Dùng Tree-sitter trích xuất:
      - definitions: các class, interface, record, enum, method (kèm signature rút gọn)
      - references: tập hợp các identifier được sử dụng trong file để tính PageRank
    """
    try:
        with open(file_path, "rb") as f:
            code_bytes = f.read()
    except Exception as e:
        return [], set()

    tree = CS_PARSER.parse(code_bytes)
    root = tree.root_node

    definitions = []
    references = set()

    def get_node_sig(node):
        """Lấy phần khai báo trước dấu ngoặc { body }"""
        body = None
        for c in node.children:
            if c.type in ("block", "declaration_list"):
                body = c
                break
        end = body.start_byte if body else node.end_byte
        return clean_signature(code_bytes[node.start_byte:end].decode("utf-8", errors="replace"))

    def walk(node, current_scope=None):
        nonlocal definitions, references

        # Thu thập reference identifier
        if node.type == "identifier":
            references.add(node.text.decode("utf-8", errors="replace"))

        # Thu thập definitions
        if node.type in ("class_declaration", "interface_declaration", "record_declaration", "enum_declaration", "struct_declaration"):
            # Lấy tên định danh
            name_node = next((c for c in node.children if c.type == "identifier"), None)
            symbol_name = name_node.text.decode("utf-8", errors="replace") if name_node else ""
            sig = get_node_sig(node)
            
            # Bỏ qua các class nội bộ hoặc rác
            if symbol_name:
                definitions.append({
                    "type": node.type,
                    "name": symbol_name,
                    "signature": sig,
                    "is_container": True
                })

            for child in node.children:
                walk(child, current_scope=symbol_name)
            return

        elif node.type in ("method_declaration", "constructor_declaration"):
            sig = get_node_sig(node)
            # Chỉ lấy các method công khai / quan trọng
            if any(k in sig for k in ("public", "protected", "internal")) or any(a in sig for a in ("[Http", "[Route")):
                name_node = next((c for c in node.children if c.type == "identifier"), None)
                symbol_name = name_node.text.decode("utf-8", errors="replace") if name_node else ""
                definitions.append({
                    "type": node.type,
                    "name": symbol_name,
                    "signature": sig,
                    "is_container": False
                })

        for child in node.children:
            walk(child, current_scope)

    walk(root)
    return definitions, references

def build_repo_graph(cs_files, root_path):
    """Xây dựng đồ thị phụ thuộc và tính PageRank cho từng file."""
    print("[*] Đang phân tích AST và xây dựng Symbol Graph...")
    file_data = {}
    symbol_to_files = defaultdict(list)

    # Bước 1: Trích xuất definitions & references
    for p in cs_files:
        rel_path = str(p.relative_to(root_path)).replace("\\", "/")
        defs, refs = extract_symbols_and_refs(p)
        file_data[rel_path] = {
            "path": rel_path,
            "defs": defs,
            "refs": refs,
            "full_path": p
        }
        for d in defs:
            if d["is_container"] and d["name"]:
                symbol_to_files[d["name"]].append(rel_path)

    # Bước 2: Dựng DiGraph (File A -> File B nếu A tham chiếu đến Symbol định nghĩa tại B)
    G = nx.DiGraph()
    for rel_path in file_data:
        G.add_node(rel_path)

    for rel_path, data in file_data.items():
        for ref in data["refs"]:
            if ref in symbol_to_files:
                for target_file in symbol_to_files[ref]:
                    if target_file != rel_path:
                        if G.has_edge(rel_path, target_file):
                            G[rel_path][target_file]["weight"] += 1
                        else:
                            G.add_edge(rel_path, target_file, weight=1)

    # Bước 3: Tính toán PageRank
    print("[*] Đang chạy thuật toán PageRank xếp hạng tầm ảnh hưởng kiến trúc...")
    try:
        pagerank_scores = nx.pagerank(G, weight="weight", alpha=0.85)
    except Exception:
        pagerank_scores = {node: 1.0 / len(G.nodes) for node in G.nodes}

    for rel_path, score in pagerank_scores.items():
        file_data[rel_path]["rank"] = score

    return file_data

def format_repo_map(file_data, token_budget=3500, all_files=False):
    """Định dạng bản đồ theo cấu trúc thư mục phân tầng, tối ưu vừa khít ngân sách token."""
    # Sắp xếp file theo điểm PageRank giảm dần
    sorted_files = sorted(file_data.values(), key=lambda x: x.get("rank", 0), reverse=True)

    # Nhóm file theo layer kiến trúc
    selected_files = set()
    total_tokens = 0

    # Ưu tiên các file có rank cao nhất
    for f in sorted_files:
        # Dự tính token của file này
        rendered_snippet = f"\n{f['path']}:\n"
        for d in f["defs"]:
            rendered_snippet += f"  {d['signature']}\n"
        
        file_tokens = len(TOKENIZER.encode(rendered_snippet))

        if not all_files and (total_tokens + file_tokens > token_budget):
            continue

        selected_files.add(f["path"])
        total_tokens += file_tokens

    print(f"[*] Đã chọn {len(selected_files)} / {len(file_data)} file phù hợp với ngân sách {token_budget} tokens (Thực tế: {total_tokens} tokens).")

    # Sắp xếp lại danh sách đã chọn theo bảng chữ cái để hiển thị cây thư mục trực quan
    selected_sorted = sorted([file_data[p] for p in selected_files], key=lambda x: x["path"])

    output_lines = []
    current_dir = None

    for item in selected_sorted:
        path_parts = item["path"].split("/")
        file_dir = "/".join(path_parts[:-1]) if len(path_parts) > 1 else ""
        file_name = path_parts[-1]

        if file_dir != current_dir:
            current_dir = file_dir
            output_lines.append(f"\n📂 {current_dir}/")

        output_lines.append(f"  📄 {file_name} (Rank: {item.get('rank', 0):.4f})")
        for d in item["defs"]:
            indent = "    " if d["is_container"] else "      "
            output_lines.append(f"{indent}{d['signature']}")

    return "\n".join(output_lines), len(selected_files), total_tokens

def main():
    args = parse_args()
    root_path = Path(args.root).resolve()
    out_path = Path(args.output)
    if not out_path.is_absolute():
        out_path = root_path / out_path

    print(f"============================================================")
    print(f"🚀 TRUSTSIM BACKEND - REPO MAP GENERATOR")
    print(f"   Root: {root_path}")
    print(f"   Output: {out_path}")
    print(f"   Token Budget: {args.tokens}")
    print(f"============================================================")

    # Quét tất cả file .cs hợp lệ
    cs_files = [
        p for p in root_path.rglob("*.cs")
        if not any(part.lower() in IGNORED_DIRS for part in p.parts)
    ]
    print(f"[*] Tìm thấy {len(cs_files)} file C# trong các tầng Domain, Application, Infrastructure, API.")

    if not cs_files:
        print("[!] Không tìm thấy file mã nguồn nào.")
        return

    file_data = build_repo_graph(cs_files, root_path)
    map_text, file_count, actual_tokens = format_repo_map(file_data, token_budget=args.tokens, all_files=args.all_files)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("# 🗺️ TRUSTSIM BACKEND - ARCHITECTURE REPO MAP\n\n")
        f.write(f"> **Tự động sinh bởi Aider Repo Map Algorithm (Tree-sitter AST + PageRank)**\n")
        f.write(f">- Tổng file phân tích: `{len(cs_files)}` file C#\n")
        f.write(f">- File hiển thị: `{file_count}` file trọng tâm\n")
        f.write(f">- Token sử dụng: `{actual_tokens}` / `{args.tokens}` tokens\n\n")
        f.write("```csharp\n")
        f.write(map_text)
        f.write("\n```\n")

    print(f"\n[✓] THÀNH CÔNG! Đã lưu bản đồ kiến trúc tại: {out_path.resolve()}")
    print(f"    Token tiêu thụ: {actual_tokens} tokens")

if __name__ == "__main__":
    main()
